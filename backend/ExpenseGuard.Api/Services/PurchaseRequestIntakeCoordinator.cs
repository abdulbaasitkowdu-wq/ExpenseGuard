using System.Text.Json;
using ExpenseGuard.Api.Contracts;
using ExpenseGuard.Api.Data;
using ExpenseGuard.Api.Models;
using ExpenseGuard.Api.Reimbursements;
using Microsoft.EntityFrameworkCore;

namespace ExpenseGuard.Api.Services;

public interface IPurchaseRequestIntakeCoordinator
{
    Task AfterSubmitAsync(int purchaseRequestId, int actorId, CancellationToken ct);
    Task EnsureWorkflowAsync(int purchaseRequestId, int actorId, CancellationToken ct);
}

public sealed class PurchaseRequestApprovalState
{
    public int TemplateId { get; set; }
    public int CurrentSequence { get; set; }
    public List<PurchaseRequestApprovalStepDto> Steps { get; set; } = [];
}

public sealed class PurchaseRequestIntakeCoordinator(
    AppDbContext db,
    IClaimReviewClient? reviews = null,
    IWorkflowLedger? workflows = null) : IPurchaseRequestIntakeCoordinator
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task AfterSubmitAsync(int purchaseRequestId, int actorId, CancellationToken ct)
    {
        var item = await db.PurchaseRequests
            .Include(p => p.Employee).ThenInclude(e => e.Department)
            .Include(p => p.Employee).ThenInclude(e => e.Designation)
            .SingleAsync(p => p.PurchaseRequestId == purchaseRequestId, ct);
        var employee = item.Employee;
        var currency = string.IsNullOrWhiteSpace(item.Currency) ? "LKR" : item.Currency.Trim().ToUpperInvariant();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;
        var correlation = $"purchase-request:{item.PurchaseRequestId}";
        var execution = workflows is null ? null : await workflows.StartPurchaseRequestAsync(
            item.PurchaseRequestId,
            $"Review purchase request {item.PurchaseRequestId} for {employee.Department.DepartmentName}",
            correlation, ct);
        if (workflows is not null)
            await workflows.AuditAsync(actorId, "purchase_request.submitted", "PurchaseRequest",
                item.PurchaseRequestId.ToString(),
                new { item.EstimatedAmount, item.Category, item.Vendor }, correlation, ct);

        var policies = await db.Policies.AsNoTracking()
            .Where(p => p.IsActive && p.Currency == currency
                && p.EffectiveFrom <= now && (p.EffectiveTo == null || p.EffectiveTo > now)
                && (p.DepartmentId == null || p.DepartmentId == employee.DepartmentId))
            .ToListAsync(ct);
        var matched = MatchPolicy(policies, item.Description, item.Vendor, item.Category);
        var category = matched?.Category ?? item.Category?.Trim() ?? "Unknown";
        item.Category = category;

        var policyFlags = EvaluatePolicy(matched, item.EstimatedAmount);
        var fraudFlags = await EvaluateFraudAsync(item, matched, ct);
        var budget = await db.Budgets.AsNoTracking()
            .Where(b => b.DepartmentId == employee.DepartmentId && b.IsActive
                && b.Currency == currency && b.PeriodStart <= today && b.PeriodEnd >= today)
            .OrderByDescending(b => b.AllocatedAmount - b.ReservedAmount - b.SpentAmount)
            .FirstOrDefaultAsync(ct);
        var available = budget?.AvailableAmount;
        var budgetFlags = new List<ReviewFlagDto>();
        if (budget is null)
            budgetFlags.Add(new("NO_ACTIVE_BUDGET", "high", "No active department budget covers this request."));
        else if (available < item.EstimatedAmount)
            budgetFlags.Add(new("BUDGET_EXCEEDED", "high",
                $"Requested {item.EstimatedAmount:0.00} {currency} exceeds remaining {available:0.00} {currency}."));

        var review = reviews is null ? null : await reviews.ReviewAsync(new ClaimReviewRequest
        {
            WorkflowId = $"purchase-request:{item.PurchaseRequestId}",
            ExpenseClaimId = item.PurchaseRequestId,
            Kind = "purchase_request",
            Objective = $"Review purchase request {item.PurchaseRequestId} for {employee.Department.DepartmentName}",
            Description = item.Description,
            ReceiptText = $"{item.Description}\n{item.Vendor}",
            Amount = item.EstimatedAmount,
            Category = category,
            Currency = currency,
            Vendor = item.Vendor,
            Policies = policies.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                .Select(p => new ClaimReviewPolicy
                {
                    PolicyCode = p.PolicyCode,
                    Category = p.Category,
                    MinAmount = p.MinAmount,
                    MaxAmount = p.MaxAmount,
                    ReceiptRequired = false
                }).ToList(),
            Budgets = budget is null ? [] :
            [
                new ClaimReviewBudget
                {
                    BudgetId = budget.BudgetId,
                    Currency = budget.Currency,
                    Allocated = budget.AllocatedAmount,
                    Reserved = budget.ReservedAmount,
                    Spent = budget.SpentAmount,
                    ReportedAvailable = budget.AvailableAmount
                }
            ]
        }, ct);

        var policySection = Section("policy", policyFlags,
            policyFlags.Any(f => f.Severity == "error") ? "non_compliant"
                : matched is null ? "not_applicable" : "compliant",
            review);
        var fraudSection = Section("fraud", fraudFlags,
            fraudFlags.Any(f => f.Severity is "critical" or "high") ? "high"
                : fraudFlags.Count > 0 ? "medium" : "low",
            review);
        var budgetSection = new ReviewSectionDto(
            budgetFlags.Count > 0 ? "exceeded" : "ok",
            AiSummary(review, "budget") ?? (budgetFlags.Count == 0
                ? "Department budget can cover this request."
                : string.Join(" ", budgetFlags.Select(f => f.Message))),
            budgetFlags,
            item.EstimatedAmount,
            available,
            budget?.AllocatedAmount);

        var hasFlags = policySection.Flags.Count > 0 || fraudSection.Flags.Count > 0 || budgetSection.Flags.Count > 0;
        var summary = hasFlags
            ? string.Join(" ", new[] { policySection.Summary, fraudSection.Summary, budgetSection.Summary }.Where(s => !string.IsNullOrWhiteSpace(s)))
            : "No policy, fraud, or budget flags. Human approval is still required.";
        var packet = new PurchaseRequestReviewDto(
            category, matched?.PolicyCode, employee.DepartmentId, employee.Department.DepartmentName,
            employee.FullName, employee.Designation?.Name, hasFlags, summary,
            policySection, fraudSection, budgetSection);

        var template = await EnsureApprovalTemplateAsync(ct);
        var stages = ApprovalStageSelector.ForAmount(template.Stages, item.EstimatedAmount);
        if (stages.Count == 0)
            throw new InvalidOperationException("No approval stages apply to this purchase request.");
        var approval = new PurchaseRequestApprovalState
        {
            TemplateId = template.ApprovalWorkflowTemplateId,
            CurrentSequence = stages[0].Sequence,
            Steps = stages.Select(s => new PurchaseRequestApprovalStepDto(
                s.Sequence, s.RequiredRole, ApprovalStatuses.Pending, null, null, null)).ToList()
        };

        item.ReviewJson = JsonSerializer.Serialize(packet, Json);
        item.ApprovalJson = JsonSerializer.Serialize(approval, Json);
        item.CurrentRequiredRole = stages[0].RequiredRole;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        if (workflows is not null && execution is not null)
        {
            await workflows.CompleteStepAsync(execution.WorkflowExecutionId, 1, "PolicyCompliance", "AGENT",
                policySection.Outcome == "non_compliant" ? "FAILED" : "COMPLETED",
                new { Policy = policySection, Ai = Finding(review, "policy") }, ct);
            await workflows.CompleteStepAsync(execution.WorkflowExecutionId, 2, "FraudRisk", "AGENT",
                fraudSection.Outcome is "high" or "critical" ? "FAILED" : "COMPLETED",
                new { Fraud = fraudSection, Ai = Finding(review, "fraud") }, ct);
            await workflows.CompleteStepAsync(execution.WorkflowExecutionId, 3, "BudgetMonitor", "AGENT",
                budgetSection.Outcome == "exceeded" ? "FAILED" : "COMPLETED",
                new { Budget = budgetSection, Ai = Finding(review, "budget") }, ct);
            await workflows.CompleteStepAsync(execution.WorkflowExecutionId, 4, "HumanApproval", "HUMAN_APPROVAL",
                "WAITING_FOR_HUMAN", new { item.PurchaseRequestId, approval.TemplateId, approval.CurrentSequence }, ct);
            await workflows.SetPurchaseRequestStatusAsync(item.PurchaseRequestId, "WAITING_FOR_APPROVAL", ct);
        }
    }

    public async Task EnsureWorkflowAsync(int purchaseRequestId, int actorId, CancellationToken ct)
    {
        if (workflows is null) return;
        if (await db.WorkflowExecutions.AnyAsync(w => w.PurchaseRequestId == purchaseRequestId, ct))
            return;
        var item = await db.PurchaseRequests
            .Include(p => p.Employee).ThenInclude(e => e.Department)
            .SingleOrDefaultAsync(p => p.PurchaseRequestId == purchaseRequestId, ct);
        var packet = ReadReview(item?.ReviewJson);
        if (item is null || packet is null) return;

        var correlation = $"purchase-request:{item.PurchaseRequestId}";
        var execution = await workflows.StartPurchaseRequestAsync(
            item.PurchaseRequestId,
            $"Review purchase request {item.PurchaseRequestId} for {item.Employee.Department.DepartmentName}",
            correlation, ct);
        await workflows.AuditAsync(actorId, "purchase_request.workflow_backfill", "PurchaseRequest",
            item.PurchaseRequestId.ToString(), new { item.Status, packet.HasFlags }, correlation, ct);
        await workflows.CompleteStepAsync(execution.WorkflowExecutionId, 1, "PolicyCompliance", "AGENT",
            packet.Policy.Outcome == "non_compliant" ? "FAILED" : "COMPLETED", packet.Policy, ct);
        await workflows.CompleteStepAsync(execution.WorkflowExecutionId, 2, "FraudRisk", "AGENT",
            packet.Fraud.Outcome is "high" or "critical" ? "FAILED" : "COMPLETED", packet.Fraud, ct);
        await workflows.CompleteStepAsync(execution.WorkflowExecutionId, 3, "BudgetMonitor", "AGENT",
            packet.Budget.Outcome == "exceeded" ? "FAILED" : "COMPLETED", packet.Budget, ct);
        await workflows.CompleteStepAsync(execution.WorkflowExecutionId, 4, "HumanApproval", "HUMAN_APPROVAL",
            item.Status == PurchaseRequestStatus.Submitted ? "WAITING_FOR_HUMAN" : item.Status.ToString().ToUpperInvariant(),
            new { item.PurchaseRequestId, item.Status }, ct);
        await workflows.SetPurchaseRequestStatusAsync(item.PurchaseRequestId, item.Status switch
        {
            PurchaseRequestStatus.Rejected => "REJECTED",
            PurchaseRequestStatus.Draft => "REVISION_REQUIRED",
            PurchaseRequestStatus.Approved => "APPROVED",
            _ => "WAITING_FOR_APPROVAL"
        }, ct);
    }

    private static object? Finding(ClaimReviewResponse? review, string agent)
        => review?.Findings.FirstOrDefault(item => item.Agent == agent);

    private static readonly Dictionary<string, string[]> CategoryAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Entertainment subscriptions"] =
        [
            "entertainment", "streaming", "netflix", "spotify", "disney", "disney+", "hulu", "hbo",
            "prime video", "youtube premium", "apple tv", "apple music", "paramount+", "crunchyroll"
        ],
        ["Personal entertainment"] =
        [
            "entertainment", "streaming", "netflix", "spotify", "disney", "hulu", "hbo", "gaming"
        ],
        ["Personal gaming equipment"] = ["gaming", "steam", "playstation", "xbox", "nintendo"],
        ["Gambling"] = ["gambling", "casino", "betting", "sportsbook"],
        ["Cryptocurrency"] = ["crypto", "cryptocurrency", "bitcoin", "ethereum", "binance", "coinbase"]
    };

    private static readonly HashSet<string> SignificantCategoryTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "entertainment", "gambling", "cryptocurrency", "gaming"
    };

    internal static Policy? MatchPolicy(IReadOnlyList<Policy> policies, string description, string? vendor, string? explicitCategory)
    {
        if (string.Equals(explicitCategory, "Unknown", StringComparison.OrdinalIgnoreCase))
            explicitCategory = null;
        var haystack = $"{description} {vendor} {explicitCategory}".ToLowerInvariant();
        var byName = policies.Where(p => MatchesPolicy(p, explicitCategory, haystack)).ToList();
        return byName
            .OrderByDescending(p => p.MaxAmount == 0)
            .ThenByDescending(p => p.DepartmentId.HasValue)
            .ThenByDescending(p => p.Category.Length)
            .ThenByDescending(p => p.Priority)
            .ThenByDescending(p => p.Version)
            .FirstOrDefault();
    }

    private static bool MatchesPolicy(Policy policy, string? explicitCategory, string haystack)
    {
        if (!string.IsNullOrWhiteSpace(explicitCategory)
            && policy.Category.Equals(explicitCategory, StringComparison.OrdinalIgnoreCase))
            return true;

        var category = policy.Category.ToLowerInvariant();
        if (haystack.Contains(category, StringComparison.Ordinal))
            return true;

        if (CategoryAliases.TryGetValue(policy.Category, out var aliases)
            && aliases.Any(alias => haystack.Contains(alias, StringComparison.Ordinal)))
            return true;

        return category.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(token => SignificantCategoryTokens.Contains(token) && haystack.Contains(token, StringComparison.Ordinal));
    }

    private static List<ReviewFlagDto> EvaluatePolicy(Policy? policy, decimal amount)
    {
        var flags = new List<ReviewFlagDto>();
        if (policy is null)
        {
            flags.Add(new("NO_APPLICABLE_POLICY", "warning", "No department or global policy matched this request."));
            return flags;
        }
        if (policy.MaxAmount is 0)
            flags.Add(new("DENIED_CATEGORY", "error", $"{policy.Category} is not an allowed purchase category."));
        else if (policy.MaxAmount is not null && amount > policy.MaxAmount)
            flags.Add(new("POLICY_CAP_EXCEEDED", "error",
                $"Amount exceeds the {policy.Category} cap of {policy.MaxAmount:0.00} {policy.Currency}."));
        if (policy.MinAmount is not null && amount < policy.MinAmount)
            flags.Add(new("BELOW_MINIMUM", "error", "Amount is below the policy minimum."));
        return flags;
    }

    private async Task<List<ReviewFlagDto>> EvaluateFraudAsync(PurchaseRequest item, Policy? policy, CancellationToken ct)
    {
        var flags = new List<ReviewFlagDto>();
        if (policy?.MaxAmount == 0)
            flags.Add(new("DENIED_CATEGORY_RISK", "critical", "Request matches a forbidden spending category."));
        if (string.IsNullOrWhiteSpace(item.Vendor))
            flags.Add(new("MISSING_VENDOR", "medium", "No vendor was provided."));
        var vendor = item.Vendor?.Trim();
        if (!string.IsNullOrWhiteSpace(vendor) && await db.PurchaseRequests.AsNoTracking().AnyAsync(p =>
                p.PurchaseRequestId != item.PurchaseRequestId
                && p.EmployeeId == item.EmployeeId
                && p.Status != PurchaseRequestStatus.Draft
                && p.Status != PurchaseRequestStatus.Cancelled
                && p.Vendor == vendor
                && p.EstimatedAmount == item.EstimatedAmount, ct))
            flags.Add(new("DUPLICATE_REQUEST", "high", "A similar request for this vendor and amount already exists."));
        var highAmount = string.Equals(item.Currency, "LKR", StringComparison.OrdinalIgnoreCase) ? 1_500_000m : 10_000m;
        if (item.EstimatedAmount >= highAmount)
            flags.Add(new("HIGH_AMOUNT_ANOMALY", "medium", "Estimated amount meets the high-value review threshold."));
        return flags;
    }

    private static ReviewSectionDto Section(string agent, IReadOnlyList<ReviewFlagDto> flags, string outcome,
        ClaimReviewResponse? review)
        => new(outcome, AiSummary(review, agent) ?? DefaultSummary(agent, flags, outcome), flags);

    private static string DefaultSummary(string agent, IReadOnlyList<ReviewFlagDto> flags, string outcome)
    {
        if (flags.Count == 0) return agent switch
        {
            "policy" => "Request is within matched policy limits.",
            "fraud" => "No deterministic fraud flags.",
            _ => "No issues detected."
        };
        return $"{outcome}: {string.Join(" ", flags.Select(f => f.Message))}";
    }

    private static string? AiSummary(ClaimReviewResponse? review, string agent)
    {
        var finding = review?.Findings.FirstOrDefault(item => item.Agent == agent);
        if (finding is null || finding.Status is "failed" or "not_implemented") return null;
        return string.IsNullOrWhiteSpace(finding.Summary) ? null : finding.Summary;
    }

    private async Task<ApprovalWorkflowTemplate> EnsureApprovalTemplateAsync(CancellationToken ct)
    {
        var existing = await db.ApprovalWorkflowTemplates
            .Include(t => t.Stages)
            .SingleOrDefaultAsync(t => t.Name == ClaimIntakeCoordinator.HighValueTemplateName && t.IsActive, ct);
        if (existing is not null) return existing;
        var template = new ApprovalWorkflowTemplate
        {
            Name = ClaimIntakeCoordinator.HighValueTemplateName,
            Stages =
            [
                new ApprovalStageDefinition { Sequence = 1, RequiredRole = RoleNames.Manager },
                new ApprovalStageDefinition { Sequence = 2, RequiredRole = RoleNames.DepartmentHead, MinimumAmount = ClaimIntakeCoordinator.HighValueThreshold },
                new ApprovalStageDefinition { Sequence = 3, RequiredRole = RoleNames.Finance }
            ]
        };
        db.ApprovalWorkflowTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return template;
    }

    public static PurchaseRequestReviewDto? ReadReview(string? json)
        => string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<PurchaseRequestReviewDto>(json, Json);

    public static PurchaseRequestApprovalState? ReadApproval(string? json)
        => string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<PurchaseRequestApprovalState>(json, Json);

    public static string WriteApproval(PurchaseRequestApprovalState state)
        => JsonSerializer.Serialize(state, Json);
}

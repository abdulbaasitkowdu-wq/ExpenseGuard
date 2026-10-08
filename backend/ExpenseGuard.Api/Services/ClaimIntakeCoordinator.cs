using ExpenseGuard.Api.Contracts;
using ExpenseGuard.Api.Data;
using ExpenseGuard.Api.DTOs;
using ExpenseGuard.Api.Infrastructure;
using ExpenseGuard.Api.Models;
using ExpenseGuard.Api.Reimbursements;
using Microsoft.EntityFrameworkCore;

namespace ExpenseGuard.Api.Services;

public interface IClaimIntakeCoordinator
{
    Task AfterSubmitAsync(int claimId, int actorId, CancellationToken ct);
}

public sealed class ClaimIntakeCoordinator(
    AppDbContext db,
    IPolicyService policies,
    IFraudService fraud,
    IBudgetService budgets,
    IReimbursementService reimbursements,
    IWorkflowLedger workflows,
    IClaimReviewClient? reviews = null) : IClaimIntakeCoordinator
{
    public const string HighValueTemplateName = "HighValueAdvertising";
    public const decimal HighValueThreshold = 10000m;

    public async Task AfterSubmitAsync(int claimId, int actorId, CancellationToken ct)
    {
        var claim = await db.ExpenseClaims
            .Include(c => c.Employee).ThenInclude(e => e.Designation)
            .Include(c => c.Receipts)
            .SingleAsync(c => c.ExpenseClaimId == claimId, ct);
        var correlation = $"claim:{claimId}";
        var execution = await workflows.StartAsync(claimId,
            $"Review {claim.Category} {claim.Flow} claim {claimId}", correlation, ct);
        await workflows.AuditAsync(actorId, "claim.submitted", "ExpenseClaim", claimId.ToString(),
            new { claim.Amount, claim.Category, claim.Flow }, correlation, ct);

        var receipt = claim.Receipts.OrderBy(r => r.ReceiptId).LastOrDefault();
        var currency = string.IsNullOrWhiteSpace(claim.Currency) ? "LKR" : claim.Currency.Trim().ToUpperInvariant();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var review = reviews is null ? null : await reviews.ReviewAsync(new ClaimReviewRequest
        {
            WorkflowId = correlation,
            ExpenseClaimId = claimId,
            Objective = $"Review {claim.Category} {claim.Flow} claim {claimId}",
            ReceiptText = receipt?.ExtractedText ?? receipt?.ExtractedVendor ?? "",
            Amount = claim.Amount,
            Category = string.IsNullOrWhiteSpace(claim.Category) ? "Unknown" : claim.Category,
            Currency = currency,
            Vendor = claim.Vendor,
            Policies = await db.Policies.AsNoTracking()
                .Where(p => p.IsActive && p.Category == claim.Category && p.Currency == currency)
                .Select(p => new ClaimReviewPolicy
                {
                    PolicyCode = p.PolicyCode,
                    Category = p.Category,
                    MinAmount = p.MinAmount,
                    MaxAmount = p.MaxAmount,
                    ReceiptRequired = p.ReceiptRequired
                }).ToListAsync(ct),
            Budgets = await db.Budgets.AsNoTracking()
                .Where(b => b.DepartmentId == claim.Employee.DepartmentId && b.IsActive
                    && b.Currency == currency && b.PeriodStart <= today && b.PeriodEnd >= today)
                .Select(b => new ClaimReviewBudget
                {
                    BudgetId = b.BudgetId,
                    Currency = b.Currency,
                    Allocated = b.AllocatedAmount,
                    Reserved = b.ReservedAmount,
                    Spent = b.SpentAmount,
                    ReportedAvailable = b.AllocatedAmount - b.ReservedAmount - b.SpentAmount
                }).ToListAsync(ct)
        }, ct);
        if (review?.Receipt is not null && receipt is not null)
        {
            ApplyAiReceipt(receipt, review.Receipt);
            await db.SaveChangesAsync(ct);
        }

        await workflows.CompleteStepAsync(execution.WorkflowExecutionId, 1, "ReceiptExtraction", "AGENT",
            receipt is null ? "FAILED" : "COMPLETED",
            new
            {
                receipt?.Sha256,
                receipt?.ExtractedAmount,
                receipt?.ExtractedVendor,
                receipt?.ProcessingStatus,
                OcrText = Truncate(receipt?.ExtractedText),
                Ai = Finding(review, "receipt")
            }, ct);

        var designation = claim.Employee.Designation?.Name;
        var policy = await policies.EvaluateAsync(new PolicyEvaluateRequest
        {
            ExpenseClaimId = claimId,
            Currency = string.IsNullOrWhiteSpace(claim.Currency) ? "LKR" : claim.Currency,
            Designation = designation,
            ReceiptAmount = receipt?.ExtractedAmount
        }, ct);
        await workflows.CompleteStepAsync(execution.WorkflowExecutionId, 2, "PolicyCompliance", "AGENT",
            policy?.Outcome == "compliant" ? "COMPLETED" : "FAILED",
            new { Policy = policy, Ai = Finding(review, "policy") }, ct);

        var fraudResult = await fraud.EvaluateAsync(new FraudEvaluateRequest
        {
            ExpenseClaimId = claimId,
            InvoiceNumber = string.IsNullOrWhiteSpace(claim.PurchaseNo) ? null : claim.PurchaseNo,
            ReceiptAmount = receipt?.ExtractedAmount
        }, ct);
        await workflows.CompleteStepAsync(execution.WorkflowExecutionId, 3, "FraudRisk", "AGENT",
            fraudResult?.RiskLevel == "critical" ? "FAILED" : "COMPLETED",
            new { Fraud = fraudResult, Ai = Finding(review, "fraud") }, ct);

        if (policy?.Outcome == "non_compliant")
        {
            await MoveClaimAsync(claim, actorId, ClaimStatus.NeedsCorrection, "Policy evaluation failed.", ct);
            await workflows.SetStatusAsync(claimId, "FAILED", ct);
            return;
        }

        var reserved = await ReserveAsync(claim, ct);
        await workflows.CompleteStepAsync(execution.WorkflowExecutionId, 4, "BudgetMonitor", "AGENT",
            reserved ? "COMPLETED" : "FAILED",
            new
            {
                reserved,
                claim.Employee.DepartmentId,
                claim.Amount,
                claim.Currency,
                Ai = Finding(review, "budget")
            }, ct);

        var reimbursement = await reimbursements.CreateAsync(
            new CreateReimbursementRequest(claimId, string.IsNullOrWhiteSpace(claim.Currency) ? "LKR" : claim.Currency),
            actorId, ct);
        if (!reserved)
        {
            var entity = await db.Reimbursements.SingleAsync(r => r.ReimbursementId == reimbursement.Id, ct);
            entity.Status = ReimbursementStatuses.BudgetReviewRequired;
            entity.FailureReason = "No active budget could reserve the claim amount.";
            await db.SaveChangesAsync(ct);
            await MoveClaimAsync(claim, actorId, ClaimStatus.UnderReview, "Budget review required.", ct);
            await workflows.SetStatusAsync(claimId, "BUDGET_REVIEW_REQUIRED", ct);
            return;
        }

        var templateId = await EnsureApprovalTemplateAsync(ct);
        await reimbursements.StartApprovalAsync(reimbursement.Id, templateId, ct);
        await workflows.CompleteStepAsync(execution.WorkflowExecutionId, 5, "HumanApproval", "HUMAN_APPROVAL",
            "WAITING_FOR_HUMAN", new { reimbursement.Id, templateId }, ct);
        await MoveClaimAsync(claim, actorId, ClaimStatus.UnderReview, "Waiting for human approval.", ct);
        await workflows.SetStatusAsync(claimId, "WAITING_FOR_APPROVAL", ct);
    }

    private async Task<bool> ReserveAsync(ExpenseClaim claim, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currency = string.IsNullOrWhiteSpace(claim.Currency) ? "LKR" : claim.Currency.Trim().ToUpperInvariant();
        var budget = await db.Budgets.Where(b => b.DepartmentId == claim.Employee.DepartmentId && b.IsActive
                && b.Currency == currency && b.PeriodStart <= today && b.PeriodEnd >= today)
            .OrderByDescending(b => b.AllocatedAmount - b.ReservedAmount - b.SpentAmount)
            .FirstOrDefaultAsync(ct);
        if (budget is null) return false;
        try
        {
            await budgets.ReserveAsync(budget.BudgetId, new BudgetAmountRequest
            {
                Amount = claim.Amount,
                IdempotencyKey = $"claim:{claim.ExpenseClaimId}:reserve",
                Reference = $"claim:{claim.ExpenseClaimId}",
                Description = "Reserved on claim submission",
                Version = budget.Version
            }, ct);
            return true;
        }
        catch (Exception ex) when (ex is InvalidBudgetOperationException or InsufficientBudgetException)
        {
            return false;
        }
    }

    private async Task<int> EnsureApprovalTemplateAsync(CancellationToken ct)
    {
        var existing = await db.ApprovalWorkflowTemplates
            .Include(t => t.Stages)
            .SingleOrDefaultAsync(t => t.Name == HighValueTemplateName && t.IsActive, ct);
        if (existing is not null) return existing.ApprovalWorkflowTemplateId;

        var template = new ApprovalWorkflowTemplate
        {
            Name = HighValueTemplateName,
            Stages =
            [
                new ApprovalStageDefinition { Sequence = 1, RequiredRole = RoleNames.Manager },
                new ApprovalStageDefinition { Sequence = 2, RequiredRole = RoleNames.DepartmentHead, MinimumAmount = HighValueThreshold },
                new ApprovalStageDefinition { Sequence = 3, RequiredRole = RoleNames.Finance }
            ]
        };
        db.ApprovalWorkflowTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return template.ApprovalWorkflowTemplateId;
    }

    private async Task MoveClaimAsync(ExpenseClaim claim, int actorId, ClaimStatus target, string reason, CancellationToken ct)
    {
        if (claim.Status == target) return;
        var from = claim.Status;
        claim.Status = target;
        claim.UpdatedAt = DateTime.UtcNow;
        claim.Version++;
        db.ClaimStatusHistories.Add(new ClaimStatusHistory
        {
            ExpenseClaimId = claim.ExpenseClaimId,
            FromStatus = from,
            ToStatus = target,
            ChangedByEmployeeId = actorId,
            Reason = reason
        });
        await db.SaveChangesAsync(ct);
    }

    private static void ApplyAiReceipt(Receipt receipt, ClaimReviewReceipt ai)
    {
        if (!string.IsNullOrWhiteSpace(ai.Vendor)) receipt.ExtractedVendor = ai.Vendor.Trim();
        if (ai.Amount is not null) receipt.ExtractedAmount = ai.Amount;
        if (ai.PurchaseDate is not null) receipt.ExtractedDate = UtcDate.ToUtc(ai.PurchaseDate);
        if (!string.IsNullOrWhiteSpace(ai.Currency)) receipt.ExtractedCurrency = ai.Currency.Trim().ToUpperInvariant();
        if (ai.Confidence is not null) receipt.Confidence = ai.Confidence;
        receipt.RequiresManualReview = ai.RequiresManualReview;
        receipt.ProcessingStatus = ai.RequiresManualReview
            ? ReceiptProcessingStatus.NeedsReview : ReceiptProcessingStatus.Processed;
    }

    private static object? Finding(ClaimReviewResponse? review, string agent)
        => review?.Findings.FirstOrDefault(item => item.Agent == agent);

    private static string? Truncate(string? text)
        => string.IsNullOrEmpty(text) ? text : text.Length <= 2000 ? text : text[..2000];
}

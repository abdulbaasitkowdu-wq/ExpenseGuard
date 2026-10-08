using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ExpenseGuard.Api.Contracts;
using ExpenseGuard.Api.Data;
using ExpenseGuard.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseGuard.Api.Services;

public interface IPolicyService
{
    Task<PolicyDto> CreateAsync(CreatePolicyRequest request, CancellationToken cancellationToken);
    Task<PolicyDto?> CreateVersionAsync(int policyId, CreatePolicyVersionRequest request, CancellationToken cancellationToken);
    Task<PageResult<PolicyDto>> ListAsync(string? category, int? departmentId, bool? active, int page, int pageSize, CancellationToken cancellationToken);
    Task<PolicyEvaluationDto?> EvaluateAsync(PolicyEvaluateRequest request, CancellationToken cancellationToken);
}

public sealed class PolicyService(AppDbContext db, TimeProvider timeProvider) : IPolicyService
{
    public async Task<PolicyDto> CreateAsync(CreatePolicyRequest request, CancellationToken cancellationToken)
    {
        var policy = Build(request, 1);
        db.Policies.Add(policy);
        await db.SaveChangesAsync(cancellationToken);
        return Map(policy);
    }

    public async Task<PolicyDto?> CreateVersionAsync(int policyId, CreatePolicyVersionRequest request, CancellationToken cancellationToken)
    {
        var current = await db.Policies.FirstOrDefaultAsync(x => x.PolicyId == policyId, cancellationToken);
        if (current is null) return null;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var nextVersion = await db.Policies
            .Where(x => x.PolicyCode == current.PolicyCode)
            .MaxAsync(x => x.Version, cancellationToken) + 1;
        if (request.Activate)
            await db.Policies.Where(x => x.PolicyCode == current.PolicyCode && x.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), cancellationToken);

        var next = Build(request, nextVersion, current.PolicyCode);
        next.IsActive = request.Activate;
        db.Policies.Add(next);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(next);
    }

    public async Task<PageResult<PolicyDto>> ListAsync(string? category, int? departmentId, bool? active, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Policies.AsNoTracking().Include(x => x.Designations).AsQueryable();
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(x => x.Category == category);
        if (departmentId is not null) query = query.Where(x => x.DepartmentId == departmentId || x.DepartmentId == null);
        if (active is not null) query = query.Where(x => x.IsActive == active);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.PolicyCode).ThenByDescending(x => x.Version)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new(items.Select(Map).ToArray(), page, pageSize, total);
    }

    public async Task<PolicyEvaluationDto?> EvaluateAsync(PolicyEvaluateRequest request, CancellationToken cancellationToken)
    {
        var claim = await db.ExpenseClaims.Include(x => x.Employee).Include(x => x.Receipts)
            .FirstOrDefaultAsync(x => x.ExpenseClaimId == request.ExpenseClaimId && x.DeletedAt == null, cancellationToken);
        if (claim is null) return null;

        var at = (request.At ?? timeProvider.GetUtcNow().UtcDateTime).ToUniversalTime();
        var currency = request.Currency.Trim().ToUpperInvariant();
        var designation = request.Designation?.Trim().ToUpperInvariant();
        var fingerprint = Fingerprint(JsonSerializer.Serialize(new
        {
            claim.ExpenseClaimId, claim.Amount, claim.Category, claim.ReceiptImg, claim.ReceiptDoc,
            Currency = currency, Designation = designation, request.ReceiptAmount, At = at
        }));
        var existing = await db.PolicyEvaluations.AsNoTracking().Include(x => x.Violations)
            .FirstOrDefaultAsync(x => x.ExpenseClaimId == claim.ExpenseClaimId && x.InputFingerprint == fingerprint, cancellationToken);
        if (existing is not null) return Map(existing);

        var candidates = await db.Policies.Include(x => x.Designations)
            .Where(x => x.IsActive && x.Category == claim.Category && x.Currency == currency
                && x.EffectiveFrom <= at && (x.EffectiveTo == null || x.EffectiveTo > at)
                && (x.DepartmentId == null || x.DepartmentId == claim.Employee.DepartmentId))
            .ToListAsync(cancellationToken);
        var policy = candidates
            .Where(x => x.Designations.Count == 0 || (designation != null && x.Designations.Any(d => d.Designation == designation)))
            .OrderByDescending(x => x.DepartmentId == claim.Employee.DepartmentId)
            .ThenByDescending(x => x.Designations.Count > 0)
            .ThenByDescending(x => x.Priority)
            .ThenByDescending(x => x.Version)
            .ThenBy(x => x.PolicyId)
            .FirstOrDefault();

        var evaluation = new PolicyEvaluation
        {
            ExpenseClaimId = claim.ExpenseClaimId,
            PolicyId = policy?.PolicyId,
            InputFingerprint = fingerprint,
            EvaluatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        if (policy is null)
            evaluation.Violations.Add(new() { RuleCode = "NO_APPLICABLE_POLICY", Message = "No applicable policy was found.", Severity = "warning" });
        else
        {
            if (policy.ReceiptRequired && !HasReceipt(claim))
                evaluation.Violations.Add(new() { RuleCode = "RECEIPT_REQUIRED", Message = "A receipt is required.", Severity = "error" });
            if (policy.MinAmount is not null && claim.Amount < policy.MinAmount)
                evaluation.Violations.Add(new() { RuleCode = "BELOW_MINIMUM", Message = "Claim is below the policy minimum.", Severity = "error", ExpectedAmount = policy.MinAmount, ActualAmount = claim.Amount });
            if (policy.MaxAmount is not null && claim.Amount > policy.MaxAmount)
                evaluation.Violations.Add(new() { RuleCode = "POLICY_CAP_EXCEEDED", Message = "Claim exceeds the policy cap.", Severity = "error", ExpectedAmount = policy.MaxAmount, ActualAmount = claim.Amount });
            if (request.ReceiptAmount is not null && request.ReceiptAmount != claim.Amount)
                evaluation.Violations.Add(new() { RuleCode = "AMOUNT_MISMATCH", Message = "Receipt amount differs from claim amount.", Severity = "error", ExpectedAmount = claim.Amount, ActualAmount = request.ReceiptAmount });
        }
        evaluation.Outcome = policy is null
            ? "not_applicable"
            : evaluation.Violations.Any(x => x.Severity == "error") ? "non_compliant" : "compliant";
        db.PolicyEvaluations.Add(evaluation);
        await db.SaveChangesAsync(cancellationToken);
        return Map(evaluation);
    }

    private Policy Build(CreatePolicyRequest request, int version, string? policyCode = null) => new()
    {
        PolicyCode = (policyCode ?? request.PolicyCode).Trim().ToUpperInvariant(),
        Version = version, Category = request.Category.Trim(), MinAmount = request.MinAmount,
        MaxAmount = request.MaxAmount, Currency = request.Currency.Trim().ToUpperInvariant(),
        ReceiptRequired = request.ReceiptRequired, IsActive = true, Priority = request.Priority,
        EffectiveFrom = request.EffectiveFrom.ToUniversalTime(), EffectiveTo = request.EffectiveTo?.ToUniversalTime(),
        DepartmentId = request.DepartmentId, CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        Designations = request.Designations.Select(x => new PolicyDesignation { Designation = x.Trim().ToUpperInvariant() })
            .Where(x => x.Designation.Length > 0).DistinctBy(x => x.Designation).ToList()
    };

    private static bool HasReceipt(ExpenseClaim claim) =>
        !string.IsNullOrWhiteSpace(claim.ReceiptImg) || !string.IsNullOrWhiteSpace(claim.ReceiptDoc) || claim.Receipts.Count > 0;

    private static string Fingerprint(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static PolicyDto Map(Policy x) => new(x.PolicyId, x.PolicyCode, x.Version, x.Category, x.MinAmount, x.MaxAmount,
        x.Currency, x.ReceiptRequired, x.IsActive, x.Priority, x.EffectiveFrom, x.EffectiveTo, x.DepartmentId,
        x.Designations.Select(d => d.Designation).ToArray());
    private static PolicyEvaluationDto Map(PolicyEvaluation x) => new(x.PolicyEvaluationId, x.ExpenseClaimId, x.PolicyId,
        x.Outcome, x.EvaluatedAt, x.Violations.Select(v => new ViolationDto(v.RuleCode, v.Message, v.Severity, v.ExpectedAmount, v.ActualAmount)).ToArray());
}

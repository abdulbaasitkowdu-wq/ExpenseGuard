using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ExpenseGuard.Api.Contracts;
using ExpenseGuard.Api.Data;
using ExpenseGuard.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseGuard.Api.Services;

public interface IFraudService
{
    Task<FraudEvaluationDto?> EvaluateAsync(FraudEvaluateRequest request, CancellationToken cancellationToken);
    Task<PageResult<FraudFlagDto>> ListAsync(string? status, string? severity, int? claimId, int page, int pageSize, CancellationToken cancellationToken);
    Task<FraudFlagDto?> ReviewAsync(int id, ReviewFraudFlagRequest request, string actor, CancellationToken cancellationToken);
    Task<FraudFlagDto?> ResolveAsync(int id, ResolveFraudFlagRequest request, string actor, CancellationToken cancellationToken);
}

public sealed class FraudService(AppDbContext db, TimeProvider timeProvider) : IFraudService
{
    public async Task<FraudEvaluationDto?> EvaluateAsync(FraudEvaluateRequest request, CancellationToken cancellationToken)
    {
        var claim = await db.ExpenseClaims.Include(x => x.Employee).Include(x => x.Receipts)
            .FirstOrDefaultAsync(x => x.ExpenseClaimId == request.ExpenseClaimId && x.DeletedAt == null, cancellationToken);
        if (claim is null) return null;
        var invoice = request.InvoiceNumber?.Trim().ToUpperInvariant();
        var fingerprint = Hash(JsonSerializer.Serialize(new
        {
            claim.ExpenseClaimId, claim.Amount, claim.PurchaseNo, claim.ReceiptImg, claim.ReceiptDoc,
            Invoice = invoice, request.ReceiptAmount
        }));
        var existing = await db.FraudEvaluations.AsNoTracking().Include(x => x.Flags)
            .FirstOrDefaultAsync(x => x.ExpenseClaimId == claim.ExpenseClaimId && x.InputFingerprint == fingerprint, cancellationToken);
        if (existing is not null) return Map(existing);

        var evaluation = new FraudEvaluation
        {
            ExpenseClaimId = claim.ExpenseClaimId, InputFingerprint = fingerprint,
            NormalizedInvoiceNumber = invoice, ClaimAmount = claim.Amount, ReceiptAmount = request.ReceiptAmount,
            EvaluatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        void Flag(string code, string severity, decimal score, string reason, object evidence) =>
            evaluation.Flags.Add(new FraudFlag
            {
                ExpenseClaimId = claim.ExpenseClaimId, RuleCode = code, Severity = severity,
                RiskScore = score, FlagReason = reason, EvidenceJson = JsonSerializer.Serialize(evidence),
                CreatedAt = timeProvider.GetUtcNow().UtcDateTime
            });

        var hasReceipt = !string.IsNullOrWhiteSpace(claim.ReceiptImg) || !string.IsNullOrWhiteSpace(claim.ReceiptDoc)
            || claim.Receipts.Count > 0;
        if (!hasReceipt) Flag("MISSING_RECEIPT", "medium", 25, "Claim has no receipt.", new { hasReceipt });
        if (request.ReceiptAmount is not null && request.ReceiptAmount != claim.Amount)
            Flag("AMOUNT_MISMATCH", "high", 45, "Receipt and claim amounts differ.", new { claimAmount = claim.Amount, receiptAmount = request.ReceiptAmount });

        if (!string.IsNullOrWhiteSpace(claim.PurchaseNo) && await db.ExpenseClaims.AsNoTracking()
            .AnyAsync(x => x.ExpenseClaimId != claim.ExpenseClaimId && x.DeletedAt == null && x.PurchaseNo == claim.PurchaseNo, cancellationToken))
            Flag("DUPLICATE_RECEIPT", "high", 55, "Purchase/receipt number is used by another claim.", new { claim.PurchaseNo });

        if (invoice is not null && await db.FraudEvaluations.AsNoTracking()
            .AnyAsync(x => x.ExpenseClaimId != claim.ExpenseClaimId && x.NormalizedInvoiceNumber == invoice, cancellationToken))
            Flag("DUPLICATE_INVOICE", "critical", 70, "Invoice number was seen on another claim.", new { invoiceNumber = invoice });

        var cap = await db.Policies.AsNoTracking()
            .Where(x => x.IsActive && x.Category == claim.Category && (x.DepartmentId == null || x.DepartmentId == claim.Employee.DepartmentId)
                && x.EffectiveFrom <= evaluation.EvaluatedAt && (x.EffectiveTo == null || x.EffectiveTo > evaluation.EvaluatedAt)
                && x.MaxAmount != null)
            .OrderByDescending(x => x.DepartmentId == claim.Employee.DepartmentId).ThenByDescending(x => x.Priority)
            .Select(x => x.MaxAmount).FirstOrDefaultAsync(cancellationToken);
        if (cap is not null && claim.Amount > cap)
            Flag("POLICY_CAP_RISK", "high", 40, "Claim exceeds the current deterministic policy cap.", new { claimAmount = claim.Amount, cap });
        else if (claim.Amount >= 10000)
            Flag("HIGH_AMOUNT_ANOMALY", "medium", 30, "Claim meets the high-amount anomaly threshold.", new { claimAmount = claim.Amount, threshold = 10000 });

        evaluation.RiskScore = Math.Min(100, evaluation.Flags.Sum(x => x.RiskScore));
        evaluation.RiskLevel = evaluation.RiskScore >= 70 ? "critical" : evaluation.RiskScore >= 40 ? "high" : evaluation.RiskScore >= 20 ? "medium" : "low";
        db.FraudEvaluations.Add(evaluation);
        await db.SaveChangesAsync(cancellationToken);
        return Map(evaluation);
    }

    public async Task<PageResult<FraudFlagDto>> ListAsync(string? status, string? severity, int? claimId, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.FraudFlags.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(severity)) query = query.Where(x => x.Severity == severity);
        if (claimId is not null) query = query.Where(x => x.ExpenseClaimId == claimId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.FraudFlagId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new(items.Select(Map).ToArray(), page, pageSize, total);
    }

    public async Task<FraudFlagDto?> ReviewAsync(int id, ReviewFraudFlagRequest request, string actor, CancellationToken cancellationToken)
    {
        var flag = await db.FraudFlags.FindAsync([id], cancellationToken);
        if (flag is null) return null;
        flag.Status = request.Status;
        flag.ResolutionNote = request.Note;
        if (request.Status is "dismissed" or "confirmed")
        {
            flag.ResolvedBy = actor;
            flag.ResolvedAt = timeProvider.GetUtcNow().UtcDateTime;
        }
        await db.SaveChangesAsync(cancellationToken);
        return Map(flag);
    }

    public async Task<FraudFlagDto?> ResolveAsync(int id, ResolveFraudFlagRequest request, string actor, CancellationToken cancellationToken)
    {
        var flag = await db.FraudFlags.FindAsync([id], cancellationToken);
        if (flag is null) return null;
        flag.Status = "resolved";
        flag.ResolutionNote = request.ResolutionNote.Trim();
        flag.ResolvedBy = actor;
        flag.ResolvedAt = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        return Map(flag);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static FraudEvaluationDto Map(FraudEvaluation x) => new(x.FraudEvaluationId, x.ExpenseClaimId, x.RiskScore, x.RiskLevel, x.EvaluatedAt, x.Flags.Select(Map).ToArray());
    private static FraudFlagDto Map(FraudFlag x) => new(x.FraudFlagId, x.ExpenseClaimId, x.RuleCode, x.Severity, x.Source,
        x.FlagReason, x.EvidenceJson, x.RiskScore, x.Status, x.ResolutionNote, x.ResolvedBy, x.ResolvedAt, x.CreatedAt);
}

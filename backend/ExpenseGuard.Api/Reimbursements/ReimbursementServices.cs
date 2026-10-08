using System.Text.Json;
using ExpenseGuard.Api.Contracts;
using ExpenseGuard.Api.Data;
using ExpenseGuard.Api.Models;
using ExpenseGuard.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace ExpenseGuard.Api.Reimbursements;

public record BudgetCheckResult(bool Available, string? Reason = null);
public interface IBudgetGateway
{
    Task<BudgetCheckResult> CheckAsync(int departmentId, decimal amount, CancellationToken cancellationToken);
    Task RecordPaymentAsync(int departmentId, decimal amount, int reimbursementId, CancellationToken cancellationToken);
    Task ReleaseReservationAsync(int departmentId, decimal amount, int reimbursementId, CancellationToken cancellationToken);
}

public sealed class UnconfiguredBudgetGateway : IBudgetGateway
{
    public Task<BudgetCheckResult> CheckAsync(int departmentId, decimal amount, CancellationToken cancellationToken) =>
        Task.FromResult(new BudgetCheckResult(false, "Budget service contract is not configured."));
    public Task RecordPaymentAsync(int departmentId, decimal amount, int reimbursementId, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Budget service contract is not configured.");
    public Task ReleaseReservationAsync(int departmentId, decimal amount, int reimbursementId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

public record PaymentRequest(string IdempotencyKey, int ReimbursementId, int EmployeeId,
    int DepartmentId, decimal Amount, string Currency);
public record PaymentResponse(bool Success, string Status, string? ExternalId = null, string? Error = null);
public interface IPaymentProvider
{
    Task<PaymentResponse> PayAsync(PaymentRequest request, CancellationToken cancellationToken);
}

public sealed class DeterministicPaymentProvider : IPaymentProvider
{
    public Task<PaymentResponse> PayAsync(PaymentRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new PaymentResponse(true, "COMPLETED",
            $"fake-{request.IdempotencyKey.GetHashCode(StringComparison.Ordinal):x8}"));
}

public record ReimbursementDto(int Id, int ExpenseClaimId, int EmployeeId, int DepartmentId,
    decimal Amount, string Currency, string Status, Guid? PaymentId, string? PaymentReference,
    string? CurrentRequiredRole = null, IReadOnlyList<PurchaseRequestApprovalStepDto>? ApprovalSteps = null);
public record CreateReimbursementRequest(int ExpenseClaimId, string Currency = "LKR");
public record ApprovalDecision(string Decision, string? Comment);

public interface IReimbursementService
{
    Task<ReimbursementDto> CreateAsync(CreateReimbursementRequest request, int employeeId, CancellationToken cancellationToken);
    Task<ReimbursementDto?> GetAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReimbursementDto>> ListForEmployeeAsync(int employeeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReimbursementDto>> FinanceQueueAsync(string? status, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReimbursementDto>> ApprovalQueueAsync(string role, CancellationToken cancellationToken);
    Task<ReimbursementDto> StartApprovalAsync(int id, int templateId, CancellationToken cancellationToken);
    Task<ReimbursementDto> DecideAsync(int id, int employeeId, string role, ApprovalDecision decision, CancellationToken cancellationToken);
    Task<ReimbursementDto> ProcessAsync(int id, CancellationToken cancellationToken);
    Task<ReimbursementDto> PayAsync(int id, CancellationToken cancellationToken);
}

public sealed class ReimbursementService(
    AppDbContext db, IBudgetGateway budgets, IPaymentProvider payments,
    ILogger<ReimbursementService> logger, IWorkflowLedger? workflows = null) : IReimbursementService
{
    public async Task<ReimbursementDto> CreateAsync(CreateReimbursementRequest request, int employeeId, CancellationToken ct)
    {
        var existing = await db.Reimbursements.Include(r => r.ExpenseClaim).ThenInclude(c => c.Employee)
            .SingleOrDefaultAsync(r => r.ExpenseClaimId == request.ExpenseClaimId, ct);
        if (existing is not null) return Map(existing)!;
        var claim = await db.ExpenseClaims.Include(c => c.Employee)
            .SingleOrDefaultAsync(c => c.ExpenseClaimId == request.ExpenseClaimId, ct)
            ?? throw new KeyNotFoundException("Expense claim not found.");
        if (claim.EmployeeId != employeeId)
            throw new UnauthorizedAccessException("Only the claim owner can request reimbursement.");
        var reimbursement = new Reimbursement
        {
            ExpenseClaimId = claim.ExpenseClaimId, ExpenseClaim = claim, Total = claim.Amount,
            Currency = request.Currency.Trim().ToUpperInvariant(), IdempotencyKey = $"reimbursement:{claim.ExpenseClaimId}"
        };
        db.Reimbursements.Add(reimbursement);
        await db.SaveChangesAsync(ct);
        return Map(reimbursement)!;
    }

    public async Task<ReimbursementDto?> GetAsync(int id, CancellationToken ct) =>
        Map(await db.Reimbursements.Include(r => r.ExpenseClaim).ThenInclude(c => c.Employee)
            .Include(r => r.ApprovalProcesses).ThenInclude(p => p.Steps)
            .SingleOrDefaultAsync(r => r.ReimbursementId == id, ct));

    public async Task<IReadOnlyList<ReimbursementDto>> ListForEmployeeAsync(int employeeId, CancellationToken ct) =>
        await db.Reimbursements.AsNoTracking().Include(r => r.ExpenseClaim).ThenInclude(c => c.Employee)
            .Where(r => r.ExpenseClaim.EmployeeId == employeeId).OrderByDescending(r => r.RequestedAt)
            .Select(r => new ReimbursementDto(r.ReimbursementId, r.ExpenseClaimId, r.ExpenseClaim.EmployeeId,
                r.ExpenseClaim.Employee.DepartmentId, r.Total, r.Currency, r.Status, r.PaymentId, r.PaymentReference, null, null))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ReimbursementDto>> FinanceQueueAsync(string? status, CancellationToken ct)
    {
        var query = db.Reimbursements.AsNoTracking().Include(r => r.ExpenseClaim).ThenInclude(c => c.Employee).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(r => r.Status == status.ToUpperInvariant());
        return await query.OrderBy(r => r.RequestedAt)
            .Select(r => new ReimbursementDto(r.ReimbursementId, r.ExpenseClaimId, r.ExpenseClaim.EmployeeId,
                r.ExpenseClaim.Employee.DepartmentId, r.Total, r.Currency, r.Status, r.PaymentId, r.PaymentReference, null, null))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ReimbursementDto>> ApprovalQueueAsync(string role, CancellationToken ct) =>
        await db.ApprovalProcesses.AsNoTracking()
            .Where(p => p.Status == ApprovalStatuses.Pending &&
                p.Steps.Any(s => s.Sequence == p.CurrentSequence && s.RequiredRole == role))
            .OrderBy(p => p.CreatedAt)
            .Select(p => new ReimbursementDto(
                p.Reimbursement.ReimbursementId,
                p.Reimbursement.ExpenseClaimId,
                p.Reimbursement.ExpenseClaim.EmployeeId,
                p.Reimbursement.ExpenseClaim.Employee.DepartmentId,
                p.Reimbursement.Total,
                p.Reimbursement.Currency,
                p.Reimbursement.Status,
                p.Reimbursement.PaymentId,
                p.Reimbursement.PaymentReference,
                p.Steps.Where(s => s.Sequence == p.CurrentSequence).Select(s => s.RequiredRole).FirstOrDefault(),
                null))
            .ToListAsync(ct);

    public async Task<ReimbursementDto> StartApprovalAsync(int id, int templateId, CancellationToken ct)
    {
        var reimbursement = await Required(id, ct);
        if (await db.ApprovalProcesses.AnyAsync(p => p.ReimbursementId == id && p.Status == ApprovalStatuses.Pending, ct))
            return Map(reimbursement)!;
        var template = await db.ApprovalWorkflowTemplates.Include(t => t.Stages)
            .SingleOrDefaultAsync(t => t.ApprovalWorkflowTemplateId == templateId && t.IsActive, ct)
            ?? throw new KeyNotFoundException("Approval workflow template not found.");
        var stages = ApprovalStageSelector.ForAmount(template.Stages, reimbursement.Total);
        if (stages.Count == 0) throw new InvalidOperationException("Workflow has no applicable approval stages.");
        var snapshot = JsonSerializer.Serialize(new { template.ApprovalWorkflowTemplateId, template.Name, Stages = stages.Select(s => new { s.Sequence, s.RequiredRole }) });
        var process = new ApprovalProcess
        {
            ReimbursementId = id, ApprovalWorkflowTemplateId = templateId,
            CurrentSequence = stages[0].Sequence, TemplateSnapshotJson = snapshot,
            Steps = stages.Select(s => new ApprovalStep
            {
                Sequence = s.Sequence, RequiredRole = s.RequiredRole,
                StageSnapshotJson = JsonSerializer.Serialize(new { s.Sequence, s.RequiredRole, s.MinimumAmount, s.MaximumAmount })
            }).ToList()
        };
        reimbursement.Status = ReimbursementStatuses.PendingApproval;
        db.ApprovalProcesses.Add(process);
        await db.SaveChangesAsync(ct);
        return Map(reimbursement)!;
    }

    public async Task<ReimbursementDto> DecideAsync(int id, int employeeId, string role, ApprovalDecision decision, CancellationToken ct)
    {
        var reimbursement = await Required(id, ct);
        var process = await db.ApprovalProcesses.Include(p => p.Steps)
            .SingleOrDefaultAsync(p => p.ReimbursementId == id && p.Status == ApprovalStatuses.Pending, ct)
            ?? throw new InvalidOperationException("No pending approval process.");
        var step = process.Steps.Single(s => s.Sequence == process.CurrentSequence);
        if (!string.Equals(step.RequiredRole, role, StringComparison.OrdinalIgnoreCase) && role != RoleNames.Admin)
            throw new UnauthorizedAccessException($"Current stage requires role {step.RequiredRole}.");
        var normalized = decision.Decision.Trim().ToUpperInvariant();
        if (normalized is not (ApprovalStatuses.Approved or ApprovalStatuses.Rejected or ApprovalStatuses.RevisionRequired))
            throw new ArgumentException("Decision must be APPROVED, REJECTED, or REVISION_REQUIRED.");
        step.Status = normalized;
        step.Comment = decision.Comment;
        step.DecidedByEmployeeId = employeeId;
        step.DecidedAt = DateTime.UtcNow;
        if (normalized != ApprovalStatuses.Approved)
        {
            process.Status = normalized;
            process.CompletedAt = DateTime.UtcNow;
            reimbursement.Status = normalized == ApprovalStatuses.Rejected
                ? ReimbursementStatuses.Rejected : ReimbursementStatuses.RevisionRequired;
        }
        else
        {
            var next = process.Steps.Where(s => s.Sequence > step.Sequence).OrderBy(s => s.Sequence).FirstOrDefault();
            if (next is null)
            {
                process.Status = ApprovalStatuses.Approved;
                process.CompletedAt = DateTime.UtcNow;
                reimbursement.Status = ReimbursementStatuses.Approved;
            }
            else process.CurrentSequence = next.Sequence;
        }
        await db.SaveChangesAsync(ct);
        await AfterDecisionAsync(reimbursement, employeeId, role, normalized, decision.Comment, ct);
        return Map(reimbursement)!;
    }

    public async Task<ReimbursementDto> ProcessAsync(int id, CancellationToken ct)
    {
        var reimbursement = await Required(id, ct);
        if (reimbursement.Status != ReimbursementStatuses.Approved)
            throw new InvalidOperationException($"Cannot process reimbursement in status {reimbursement.Status}.");
        var check = await budgets.CheckAsync(reimbursement.ExpenseClaim.Employee.DepartmentId, reimbursement.Total, ct);
        if (!check.Available)
        {
            reimbursement.Status = ReimbursementStatuses.BudgetReviewRequired;
            reimbursement.FailureReason = check.Reason;
        }
        else
        {
            reimbursement.Status = ReimbursementStatuses.Processing;
            reimbursement.ProcessedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return Map(reimbursement)!;
    }

    public async Task<ReimbursementDto> PayAsync(int id, CancellationToken ct)
    {
        var reimbursement = await Required(id, ct);
        if (reimbursement.Status == ReimbursementStatuses.Paid) return Map(reimbursement)!;
        if (reimbursement.Status != ReimbursementStatuses.Processing)
            throw new InvalidOperationException($"Cannot pay reimbursement in status {reimbursement.Status}.");
        var key = string.IsNullOrWhiteSpace(reimbursement.IdempotencyKey) ? $"reimbursement:{id}" : reimbursement.IdempotencyKey;
        var existing = await db.PaymentTransactions.SingleOrDefaultAsync(p => p.IdempotencyKey == key, ct);
        if (existing is not null)
        {
            if (existing.Status == "COMPLETED")
            {
                reimbursement.Status = ReimbursementStatuses.Paid;
                reimbursement.PaymentId = existing.PaymentTransactionId;
                reimbursement.PaymentReference = existing.ExternalTransactionId;
                await db.SaveChangesAsync(ct);
            }
            return Map(reimbursement)!;
        }
        reimbursement.Status = ReimbursementStatuses.PaymentPending;
        var response = await payments.PayAsync(new PaymentRequest(key, id, reimbursement.ExpenseClaim.EmployeeId,
            reimbursement.ExpenseClaim.Employee.DepartmentId, reimbursement.Total, reimbursement.Currency), ct);
        var transaction = new PaymentTransaction
        {
            ReimbursementId = id, IdempotencyKey = key, Amount = reimbursement.Total,
            Currency = reimbursement.Currency, Status = response.Status,
            ExternalTransactionId = response.ExternalId, FailureReason = response.Error
        };
        db.PaymentTransactions.Add(transaction);
        reimbursement.PaymentId = transaction.PaymentTransactionId;
        if (response.Success && response.Status == "COMPLETED")
        {
            reimbursement.Status = ReimbursementStatuses.Paid;
            reimbursement.PaymentReference = response.ExternalId;
            reimbursement.PaymentProvider = payments.GetType().Name;
            reimbursement.CompletedAt = DateTime.UtcNow;
            await budgets.RecordPaymentAsync(reimbursement.ExpenseClaim.Employee.DepartmentId,
                reimbursement.Total, id, ct);
        }
        else
        {
            reimbursement.Status = ReimbursementStatuses.PaymentFailed;
            reimbursement.FailureReason = response.Error;
            reimbursement.RetryCount++;
        }
        await db.SaveChangesAsync(ct);
        if (reimbursement.Status == ReimbursementStatuses.Paid && workflows is not null)
            await workflows.SetStatusAsync(reimbursement.ExpenseClaimId, "COMPLETED", ct);
        logger.LogInformation("Payment {PaymentStatus} for reimbursement {ReimbursementId}", reimbursement.Status, id);
        return Map(reimbursement)!;
    }

    private async Task AfterDecisionAsync(Reimbursement reimbursement, int employeeId, string role,
        string decision, string? comment, CancellationToken ct)
    {
        if (workflows is not null)
        {
            await workflows.AuditAsync(employeeId, $"approval.{decision.ToLowerInvariant()}", "Reimbursement",
                reimbursement.ReimbursementId.ToString(), new { role, decision, comment },
                $"reimbursement:{reimbursement.ReimbursementId}", ct);
        }

        var claim = reimbursement.ExpenseClaim;
        if (decision == ApprovalStatuses.Rejected)
        {
            await MoveClaimAsync(claim, employeeId, ClaimStatus.Rejected, comment ?? "Rejected during approval.", ct);
            await budgets.ReleaseReservationAsync(claim.Employee.DepartmentId, reimbursement.Total,
                reimbursement.ReimbursementId, ct);
            if (workflows is not null) await workflows.SetStatusAsync(claim.ExpenseClaimId, "REJECTED", ct);
        }
        else if (decision == ApprovalStatuses.RevisionRequired)
        {
            await MoveClaimAsync(claim, employeeId, ClaimStatus.NeedsCorrection, comment ?? "Revision required.", ct);
            await budgets.ReleaseReservationAsync(claim.Employee.DepartmentId, reimbursement.Total,
                reimbursement.ReimbursementId, ct);
            if (workflows is not null) await workflows.SetStatusAsync(claim.ExpenseClaimId, "REVISION_REQUIRED", ct);
        }
        else if (reimbursement.Status == ReimbursementStatuses.Approved)
        {
            await MoveClaimAsync(claim, employeeId, ClaimStatus.Approved, "All approval stages completed.", ct);
            if (workflows is not null) await workflows.SetStatusAsync(claim.ExpenseClaimId, "APPROVED", ct);
        }
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

    private async Task<Reimbursement> Required(int id, CancellationToken ct) =>
        await db.Reimbursements.Include(r => r.ExpenseClaim).ThenInclude(c => c.Employee)
            .Include(r => r.ApprovalProcesses).ThenInclude(p => p.Steps)
            .SingleOrDefaultAsync(r => r.ReimbursementId == id, ct)
        ?? throw new KeyNotFoundException("Reimbursement not found.");

    private static ReimbursementDto? Map(Reimbursement? r)
    {
        if (r is null) return null;
        var process = r.ApprovalProcesses?.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
        var current = process is { Status: ApprovalStatuses.Pending }
            ? process.Steps.FirstOrDefault(s => s.Sequence == process.CurrentSequence)?.RequiredRole
            : null;
        var steps = process?.Steps.OrderBy(s => s.Sequence)
            .Select(s => new PurchaseRequestApprovalStepDto(s.Sequence, s.RequiredRole, s.Status, s.Comment, s.DecidedByEmployeeId, s.DecidedAt))
            .ToList();
        return new(r.ReimbursementId, r.ExpenseClaimId, r.ExpenseClaim.EmployeeId,
            r.ExpenseClaim.Employee.DepartmentId, r.Total, r.Currency, r.Status, r.PaymentId, r.PaymentReference,
            current, steps);
    }
}

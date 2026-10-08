using System.Security.Claims;
using ExpenseGuard.Api.Contracts;
using ExpenseGuard.Api.Data;
using ExpenseGuard.Api.Infrastructure;
using ExpenseGuard.Api.Models;
using ExpenseGuard.Api.Reimbursements;
using Microsoft.EntityFrameworkCore;

namespace ExpenseGuard.Api.Services;

public interface ICurrentEmployee
{
    int EmployeeId { get; }
}

public sealed class HeaderCurrentEmployee(IHttpContextAccessor accessor) : ICurrentEmployee
{
    public int EmployeeId
    {
        get
        {
            var user = accessor.HttpContext?.User;
            if (int.TryParse(user?.FindFirstValue(ClaimTypes.NameIdentifier), out var jwtId) && jwtId > 0)
                return jwtId;
            if (int.TryParse(accessor.HttpContext?.Request.Headers["X-Employee-Id"], out var id) && id > 0)
                return id;
            throw new UnauthorizedAccessException("Authenticated employee identity is required.");
        }
    }
}

public interface IEmployeeService
{
    Task<EmployeeProfileDto> GetProfileAsync(int actorId, CancellationToken ct);
    Task<IReadOnlyList<EmployeeProfileDto>> ListAsync(int actorId, CancellationToken ct);
}

public sealed class EmployeeService(AppDbContext db) : IEmployeeService
{
    private static EmployeeProfileDto Map(Employee e) => new(
        e.EmployeeId, e.FullName, e.Email, e.Username, e.IsActive, e.IsLocked, e.RoleId,
        e.DepartmentId, e.Department?.DepartmentName, e.ManagerId, e.DesignationId,
        e.Designation == null ? null : e.Designation.Name);

    public async Task<EmployeeProfileDto> GetProfileAsync(int actorId, CancellationToken ct)
        => Map(await db.Employees.AsNoTracking().Include(e => e.Designation).Include(e => e.Department)
            .SingleOrDefaultAsync(e => e.EmployeeId == actorId, ct)
            ?? throw new KeyNotFoundException("Employee not found."));

    public async Task<IReadOnlyList<EmployeeProfileDto>> ListAsync(int actorId, CancellationToken ct)
    {
        var isAdmin = await db.Employees.AsNoTracking().AnyAsync(
            e => e.EmployeeId == actorId && e.IsActive && !e.IsLocked && e.Role.RoleName.ToLower() == "admin", ct);
        if (!isAdmin) throw new UnauthorizedAccessException("Administrator role is required.");
        return await db.Employees.AsNoTracking().Include(e => e.Designation).Include(e => e.Department)
            .OrderBy(e => e.FullName)
            .Select(e => new EmployeeProfileDto(e.EmployeeId, e.FullName, e.Email, e.Username,
                e.IsActive, e.IsLocked, e.RoleId, e.DepartmentId, e.Department.DepartmentName, e.ManagerId, e.DesignationId,
                e.Designation == null ? null : e.Designation.Name)).ToListAsync(ct);
    }
}

public interface IPurchaseRequestService
{
    Task<IReadOnlyList<PurchaseRequestDto>> ListAsync(int actorId, CancellationToken ct);
    Task<PurchaseRequestDto> GetAsync(int id, int actorId, CancellationToken ct);
    Task<PurchaseRequestDto> CreateAsync(PurchaseRequestWriteDto input, int actorId, CancellationToken ct);
    Task<PurchaseRequestDto> UpdateAsync(int id, PurchaseRequestWriteDto input, int actorId, CancellationToken ct);
    Task SubmitAsync(int id, int actorId, CancellationToken ct);
    Task DeleteAsync(int id, int actorId, CancellationToken ct);
    Task<IReadOnlyList<PurchaseRequestDto>> ApprovalQueueAsync(string role, CancellationToken ct);
    Task<PurchaseRequestDto> GetForApprovalAsync(int id, int actorId, string role, CancellationToken ct);
    Task<PurchaseRequestDto> DecideAsync(int id, int actorId, string role, ApprovalDecision decision, CancellationToken ct);
    Task<IReadOnlyList<PurchaseRequestHistoryDto>> HistoryAsync(int id, int actorId, string role, CancellationToken ct);
}

public sealed class PurchaseRequestService(
    AppDbContext db,
    IPurchaseRequestIntakeCoordinator? intake = null,
    IWorkflowLedger? workflows = null) : IPurchaseRequestService
{
    private IQueryable<PurchaseRequest> Tracked() => db.PurchaseRequests
        .Include(p => p.Employee).ThenInclude(e => e.Department)
        .Include(p => p.Employee).ThenInclude(e => e.Designation);

    private static PurchaseRequestDto Map(PurchaseRequest p)
    {
        var approval = PurchaseRequestIntakeCoordinator.ReadApproval(p.ApprovalJson);
        return new(p.PurchaseRequestId, p.EmployeeId, p.Employee.FullName, p.Employee.Department?.DepartmentName,
            p.Employee.Designation?.Name, p.Description, p.EstimatedAmount, p.Currency, p.Vendor, p.Category,
            p.Status, p.SubmittedAt, p.Version, p.CurrentRequiredRole,
            PurchaseRequestIntakeCoordinator.ReadReview(p.ReviewJson), approval?.Steps ?? []);
    }

    private async Task<PurchaseRequest> Owned(int id, int actorId, CancellationToken ct)
    {
        var item = await Tracked().SingleOrDefaultAsync(p => p.PurchaseRequestId == id, ct)
            ?? throw new KeyNotFoundException("Purchase request not found.");
        if (item.EmployeeId != actorId) throw new UnauthorizedAccessException("Purchase request is owned by another employee.");
        return item;
    }

    public async Task<IReadOnlyList<PurchaseRequestDto>> ListAsync(int actorId, CancellationToken ct)
        => (await Tracked().AsNoTracking().Where(p => p.EmployeeId == actorId)
            .OrderByDescending(p => p.CreatedAt).ToListAsync(ct)).Select(Map).ToList();

    public async Task<PurchaseRequestDto> GetAsync(int id, int actorId, CancellationToken ct) => Map(await Owned(id, actorId, ct));

    public async Task<PurchaseRequestDto> CreateAsync(PurchaseRequestWriteDto input, int actorId, CancellationToken ct)
    {
        var item = new PurchaseRequest { EmployeeId = actorId };
        Apply(item, input);
        RecordHistory(item, PurchaseRequestStatus.Draft, PurchaseRequestStatus.Draft, actorId, "Created");
        db.Add(item);
        await db.SaveChangesAsync(ct);
        return Map(await Tracked().SingleAsync(p => p.PurchaseRequestId == item.PurchaseRequestId, ct));
    }

    public async Task<PurchaseRequestDto> UpdateAsync(int id, PurchaseRequestWriteDto input, int actorId, CancellationToken ct)
    {
        var item = await Owned(id, actorId, ct);
        if (item.Status != PurchaseRequestStatus.Draft) throw new InvalidOperationException("Only draft purchase requests can be edited.");
        if (item.Version != input.Version) throw new DbUpdateConcurrencyException("Purchase request has changed.");
        Apply(item, input);
        item.Version++;
        await db.SaveChangesAsync(ct);
        return Map(item);
    }

    public async Task SubmitAsync(int id, int actorId, CancellationToken ct)
    {
        var item = await Owned(id, actorId, ct);
        if (item.Status != PurchaseRequestStatus.Draft) throw new InvalidOperationException("Only draft purchase requests can be submitted.");
        RecordHistory(item, item.Status, PurchaseRequestStatus.Submitted, actorId, "Submitted");
        item.Status = PurchaseRequestStatus.Submitted;
        item.SubmittedAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;
        item.Version++;
        await db.SaveChangesAsync(ct);
        if (intake is not null) await intake.AfterSubmitAsync(id, actorId, ct);
    }

    public async Task DeleteAsync(int id, int actorId, CancellationToken ct)
    {
        var item = await Owned(id, actorId, ct);
        if (item.Status != PurchaseRequestStatus.Draft) throw new InvalidOperationException("Only draft purchase requests can be deleted.");
        db.Remove(item);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PurchaseRequestDto>> ApprovalQueueAsync(string role, CancellationToken ct)
    {
        var query = Tracked().AsNoTracking().Where(p => p.Status == PurchaseRequestStatus.Submitted && p.CurrentRequiredRole != null);
        if (!string.Equals(role, RoleNames.Admin, StringComparison.OrdinalIgnoreCase))
            query = query.Where(p => p.CurrentRequiredRole == role);
        return (await query.OrderBy(p => p.SubmittedAt).ToListAsync(ct)).Select(Map).ToList();
    }

    public async Task<PurchaseRequestDto> GetForApprovalAsync(int id, int actorId, string role, CancellationToken ct)
    {
        var item = await Tracked().SingleOrDefaultAsync(p => p.PurchaseRequestId == id, ct)
            ?? throw new KeyNotFoundException("Purchase request not found.");
        if (item.EmployeeId == actorId) return Map(item);
        var canApprove = role is RoleNames.Manager or RoleNames.DepartmentHead or RoleNames.Finance or RoleNames.Admin;
        if (!canApprove) throw new UnauthorizedAccessException("Purchase request is owned by another employee.");
        var approval = PurchaseRequestIntakeCoordinator.ReadApproval(item.ApprovalJson);
        var inQueue = item.CurrentRequiredRole == role || role == RoleNames.Admin;
        var decided = approval?.Steps.Any(s => s.DecidedByEmployeeId == actorId) == true;
        if (!inQueue && !decided)
            throw new UnauthorizedAccessException("This purchase request is not in your approval queue.");
        return Map(item);
    }

    public async Task<PurchaseRequestDto> DecideAsync(int id, int actorId, string role, ApprovalDecision decision, CancellationToken ct)
    {
        var item = await Tracked().SingleOrDefaultAsync(p => p.PurchaseRequestId == id, ct)
            ?? throw new KeyNotFoundException("Purchase request not found.");
        if (item.Status != PurchaseRequestStatus.Submitted)
            throw new InvalidOperationException("Only submitted purchase requests can be decided.");
        var from = item.Status;
        var approval = PurchaseRequestIntakeCoordinator.ReadApproval(item.ApprovalJson)
            ?? throw new InvalidOperationException("Purchase request has no approval process.");
        var step = approval.Steps.Single(s => s.Sequence == approval.CurrentSequence);
        if (!string.Equals(step.RequiredRole, role, StringComparison.OrdinalIgnoreCase) && role != RoleNames.Admin)
            throw new UnauthorizedAccessException($"Current stage requires role {step.RequiredRole}.");
        var normalized = decision.Decision.Trim().ToUpperInvariant();
        if (normalized is not (ApprovalStatuses.Approved or ApprovalStatuses.Rejected or ApprovalStatuses.RevisionRequired))
            throw new ArgumentException("Decision must be APPROVED, REJECTED, or REVISION_REQUIRED.");
        var updated = approval.Steps.Select(s => s.Sequence == step.Sequence
            ? s with { Status = normalized, Comment = decision.Comment, DecidedByEmployeeId = actorId, DecidedAt = DateTime.UtcNow }
            : s).ToList();
        if (normalized != ApprovalStatuses.Approved)
        {
            item.Status = normalized == ApprovalStatuses.Rejected ? PurchaseRequestStatus.Rejected : PurchaseRequestStatus.Draft;
            item.CurrentRequiredRole = null;
            item.ApprovalJson = PurchaseRequestIntakeCoordinator.WriteApproval(new PurchaseRequestApprovalState
            {
                TemplateId = approval.TemplateId, CurrentSequence = step.Sequence, Steps = updated
            });
        }
        else
        {
            var next = updated.Where(s => s.Sequence > step.Sequence).OrderBy(s => s.Sequence).FirstOrDefault();
            if (next is null)
            {
                item.Status = PurchaseRequestStatus.Approved;
                item.CurrentRequiredRole = null;
                item.ApprovalJson = PurchaseRequestIntakeCoordinator.WriteApproval(new PurchaseRequestApprovalState
                {
                    TemplateId = approval.TemplateId, CurrentSequence = step.Sequence, Steps = updated
                });
            }
            else
            {
                item.CurrentRequiredRole = next.RequiredRole;
                item.ApprovalJson = PurchaseRequestIntakeCoordinator.WriteApproval(new PurchaseRequestApprovalState
                {
                    TemplateId = approval.TemplateId, CurrentSequence = next.Sequence, Steps = updated
                });
            }
        }
        item.UpdatedAt = DateTime.UtcNow;
        item.Version++;
        RecordHistory(item, from, item.Status, actorId, decision.Comment ?? normalized);
        await db.SaveChangesAsync(ct);
        if (workflows is not null)
        {
            var workflowStatus = item.Status switch
            {
                PurchaseRequestStatus.Rejected => "REJECTED",
                PurchaseRequestStatus.Draft => "REVISION_REQUIRED",
                PurchaseRequestStatus.Approved => "APPROVED",
                _ => "WAITING_FOR_APPROVAL"
            };
            await workflows.SetPurchaseRequestStatusAsync(id, workflowStatus, ct);
        }
        return Map(item);
    }

    public async Task<IReadOnlyList<PurchaseRequestHistoryDto>> HistoryAsync(int id, int actorId, string role, CancellationToken ct)
    {
        var item = await db.PurchaseRequests.AsNoTracking().SingleOrDefaultAsync(p => p.PurchaseRequestId == id, ct)
            ?? throw new KeyNotFoundException("Purchase request not found.");
        if (!CanReadRequestHistory(item.EmployeeId, actorId, role))
            throw new UnauthorizedAccessException("Purchase request history is restricted.");
        return await db.PurchaseRequestStatusHistories.AsNoTracking().Where(h => h.PurchaseRequestId == id)
            .OrderBy(h => h.ChangedAt)
            .Select(h => new PurchaseRequestHistoryDto(h.PurchaseRequestStatusHistoryId, h.FromStatus, h.ToStatus,
                h.ChangedByEmployeeId, h.Reason, h.ChangedAt)).ToListAsync(ct);
    }

    private static void RecordHistory(PurchaseRequest item, PurchaseRequestStatus from, PurchaseRequestStatus to,
        int actorId, string? reason) =>
        item.StatusHistory.Add(new PurchaseRequestStatusHistory
        {
            FromStatus = from, ToStatus = to, ChangedByEmployeeId = actorId, Reason = reason
        });

    private static bool CanReadRequestHistory(int ownerId, int actorId, string role) =>
        ownerId == actorId || role is RoleNames.Finance or RoleNames.Admin;

    private static void Apply(PurchaseRequest item, PurchaseRequestWriteDto input)
    {
        item.Description = input.Description.Trim();
        item.EstimatedAmount = input.EstimatedAmount;
        item.Currency = input.Currency.ToUpperInvariant();
        item.Vendor = input.Vendor?.Trim();
        item.Category = string.IsNullOrWhiteSpace(input.Category) ? item.Category : input.Category.Trim();
        item.UpdatedAt = DateTime.UtcNow;
    }
}

public interface IClaimService
{
    Task<IReadOnlyList<ClaimDto>> SearchAsync(ClaimSearchQuery query, int actorId, CancellationToken ct);
    Task<ClaimDto> GetAsync(int id, int actorId, CancellationToken ct);
    Task<ClaimDto> CreateAsync(ClaimWriteDto input, int actorId, CancellationToken ct);
    Task<ClaimDto> UpdateAsync(int id, ClaimWriteDto input, int actorId, CancellationToken ct);
    Task TransitionAsync(int id, int actorId, ClaimStatus target, string? reason, CancellationToken ct);
    Task DeleteAsync(int id, int actorId, CancellationToken ct);
    Task<IReadOnlyList<ClaimHistoryDto>> HistoryAsync(int id, int actorId, string role, CancellationToken ct);
}

public sealed class ClaimService(AppDbContext db, IClaimIntakeCoordinator? coordinator = null) : IClaimService
{
    private static ClaimDto Map(ExpenseClaim c, string? currentRole = null,
        IReadOnlyList<PurchaseRequestApprovalStepDto>? steps = null) =>
        new(c.ExpenseClaimId, c.EmployeeId, c.PurchaseRequestId, c.Amount, c.Category, c.Description,
            c.Currency, c.Vendor, c.PurchaseDate, c.Flow, c.Status, c.Version, currentRole, steps ?? []);

    private async Task<ExpenseClaim> Owned(int id, int actorId, CancellationToken ct)
    {
        var claim = await db.ExpenseClaims.SingleOrDefaultAsync(c => c.ExpenseClaimId == id && c.DeletedAt == null, ct)
            ?? throw new KeyNotFoundException("Claim not found.");
        if (claim.EmployeeId != actorId) throw new UnauthorizedAccessException("Claim is owned by another employee.");
        return claim;
    }

    public async Task<IReadOnlyList<ClaimDto>> SearchAsync(ClaimSearchQuery query, int actorId, CancellationToken ct)
    {
        var items = db.ExpenseClaims.AsNoTracking().Where(c => c.EmployeeId == actorId && c.DeletedAt == null);
        if (query.Status is not null) items = items.Where(c => c.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.Category)) items = items.Where(c => c.Category == query.Category);
        if (query.From is not null) items = items.Where(c => c.CreatedAt >= query.From);
        if (query.To is not null) items = items.Where(c => c.CreatedAt <= query.To);
        var claims = await items.OrderByDescending(c => c.CreatedAt).Take(query.Limit).ToListAsync(ct);
        return await WithApprovalAsync(claims, ct);
    }

    public async Task<ClaimDto> GetAsync(int id, int actorId, CancellationToken ct)
        => (await WithApprovalAsync([await Owned(id, actorId, ct)], ct)).Single();

    public async Task<ClaimDto> CreateAsync(ClaimWriteDto input, int actorId, CancellationToken ct)
    {
        await ValidateLink(input, actorId, ct);
        var claim = new ExpenseClaim { EmployeeId = actorId };
        Apply(claim, input);
        db.Add(claim);
        await db.SaveChangesAsync(ct);
        return Map(claim);
    }

    public async Task<ClaimDto> UpdateAsync(int id, ClaimWriteDto input, int actorId, CancellationToken ct)
    {
        var claim = await Owned(id, actorId, ct);
        if (claim.Status is not (ClaimStatus.Draft or ClaimStatus.NeedsCorrection))
            throw new InvalidOperationException("Only draft or correction claims can be edited.");
        if (claim.Version != input.Version) throw new DbUpdateConcurrencyException("Claim has changed.");
        await ValidateLink(input, actorId, ct);
        Apply(claim, input);
        claim.Version++;
        await db.SaveChangesAsync(ct);
        return Map(claim);
    }

    public async Task TransitionAsync(int id, int actorId, ClaimStatus target, string? reason, CancellationToken ct)
    {
        var claim = await Owned(id, actorId, ct);
        var allowed = target == ClaimStatus.Submitted &&
            claim.Status is ClaimStatus.Draft or ClaimStatus.NeedsCorrection;
        if (!allowed) throw new InvalidOperationException($"Cannot transition claim from {claim.Status} to {target}.");
        if (!await db.Receipts.AnyAsync(r => r.ExpenseClaimId == id, ct))
            throw new InvalidOperationException("At least one receipt is required before submission.");
        var from = claim.Status;
        claim.Status = target;
        claim.SubmittedAt = DateTime.UtcNow;
        claim.UpdatedAt = DateTime.UtcNow;
        claim.Version++;
        db.ClaimStatusHistories.Add(new ClaimStatusHistory
        {
            ExpenseClaimId = id, FromStatus = from, ToStatus = target,
            ChangedByEmployeeId = actorId, Reason = reason
        });
        await db.SaveChangesAsync(ct);
        if (target == ClaimStatus.Submitted && coordinator is not null)
            await coordinator.AfterSubmitAsync(id, actorId, ct);
    }

    public async Task DeleteAsync(int id, int actorId, CancellationToken ct)
    {
        var claim = await Owned(id, actorId, ct);
        if (claim.Status != ClaimStatus.Draft) throw new InvalidOperationException("Only draft claims can be deleted.");
        claim.DeletedAt = DateTime.UtcNow;
        claim.Version++;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ClaimHistoryDto>> HistoryAsync(int id, int actorId, string role, CancellationToken ct)
    {
        var claim = await db.ExpenseClaims.AsNoTracking()
            .SingleOrDefaultAsync(c => c.ExpenseClaimId == id && c.DeletedAt == null, ct)
            ?? throw new KeyNotFoundException("Claim not found.");
        if (claim.EmployeeId != actorId && role is not (RoleNames.Finance or RoleNames.Admin))
            throw new UnauthorizedAccessException("Claim history is restricted.");
        return await db.ClaimStatusHistories.AsNoTracking().Where(h => h.ExpenseClaimId == id)
            .OrderBy(h => h.ChangedAt).Select(h => new ClaimHistoryDto(h.ClaimStatusHistoryId,
                h.FromStatus, h.ToStatus, h.ChangedByEmployeeId, h.Reason, h.ChangedAt)).ToListAsync(ct);
    }

    private async Task<List<ClaimDto>> WithApprovalAsync(IReadOnlyList<ExpenseClaim> claims, CancellationToken ct)
    {
        var ids = claims.Select(c => c.ExpenseClaimId).ToArray();
        var processes = await db.ApprovalProcesses.AsNoTracking().Include(p => p.Steps)
            .Include(p => p.Reimbursement)
            .Where(p => ids.Contains(p.Reimbursement.ExpenseClaimId))
            .ToListAsync(ct);
        return claims.Select(claim =>
        {
            var process = processes
                .Where(p => p.Reimbursement.ExpenseClaimId == claim.ExpenseClaimId)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();
            var current = process is { Status: ApprovalStatuses.Pending }
                ? process.Steps.FirstOrDefault(s => s.Sequence == process.CurrentSequence)?.RequiredRole
                : null;
            var steps = process?.Steps.OrderBy(s => s.Sequence)
                .Select(s => new PurchaseRequestApprovalStepDto(s.Sequence, s.RequiredRole, s.Status, s.Comment, s.DecidedByEmployeeId, s.DecidedAt))
                .ToList();
            return Map(claim, current, steps);
        }).ToList();
    }

    private async Task ValidateLink(ClaimWriteDto input, int actorId, CancellationToken ct)
    {
        if (input.PurchaseRequestId is null) return;
        var request = await db.PurchaseRequests.AsNoTracking()
            .SingleOrDefaultAsync(p => p.PurchaseRequestId == input.PurchaseRequestId, ct)
            ?? throw new InvalidOperationException("Purchase request does not exist.");
        if (request.EmployeeId != actorId) throw new UnauthorizedAccessException("Purchase request is owned by another employee.");
        if (request.Status != PurchaseRequestStatus.Approved)
            throw new InvalidOperationException("Only approved purchase requests can be linked.");
    }

    private static void Apply(ExpenseClaim claim, ClaimWriteDto input)
    {
        claim.Amount = input.Amount;
        claim.Category = input.Category.Trim();
        claim.Description = input.Description.Trim();
        claim.Currency = input.Currency.ToUpperInvariant();
        claim.Vendor = input.Vendor?.Trim();
        claim.PurchaseDate = UtcDate.ToUtc(input.PurchaseDate);
        claim.PurchaseRequestId = input.PurchaseRequestId;
        claim.Flow = input.Flow;
        claim.UpdatedAt = DateTime.UtcNow;
    }
}

public interface IRequestHistoryService
{
    Task<IReadOnlyList<RequestHistoryDto>> ListAsync(RequestHistoryQuery query, CancellationToken ct);
}

public sealed class RequestHistoryService(AppDbContext db) : IRequestHistoryService
{
    public async Task<IReadOnlyList<RequestHistoryDto>> ListAsync(RequestHistoryQuery query, CancellationToken ct)
    {
        var kind = query.Kind?.Trim().ToLowerInvariant();
        var take = Math.Clamp(query.Limit, 1, 200);
        var claims = kind is null or "claim" or "reimbursement"
            ? await db.ClaimStatusHistories.AsNoTracking()
                .Where(h => query.EmployeeId == null || h.ExpenseClaim.EmployeeId == query.EmployeeId)
                .Where(h => query.From == null || h.ChangedAt >= query.From)
                .Where(h => query.To == null || h.ChangedAt <= query.To)
                .OrderByDescending(h => h.ChangedAt).Take(take)
                .Select(h => new RequestHistoryDto(
                    "claim", h.ExpenseClaimId, h.ExpenseClaim.EmployeeId, h.ExpenseClaim.Employee.FullName,
                    h.ExpenseClaim.Employee.Department != null ? h.ExpenseClaim.Employee.Department.DepartmentName : null,
                    h.ExpenseClaim.Vendor, h.ExpenseClaim.Category,
                    h.ExpenseClaim.Amount, h.ExpenseClaim.Currency, h.FromStatus.ToString(), h.ToStatus.ToString(),
                    h.ChangedByEmployeeId, h.Reason, h.ChangedAt))
                .ToListAsync(ct)
            : [];
        var requests = kind is null or "purchase_request"
            ? await db.PurchaseRequestStatusHistories.AsNoTracking()
                .Where(h => query.EmployeeId == null || h.PurchaseRequest.EmployeeId == query.EmployeeId)
                .Where(h => query.From == null || h.ChangedAt >= query.From)
                .Where(h => query.To == null || h.ChangedAt <= query.To)
                .OrderByDescending(h => h.ChangedAt).Take(take)
                .Select(h => new RequestHistoryDto(
                    "purchase_request", h.PurchaseRequestId, h.PurchaseRequest.EmployeeId, h.PurchaseRequest.Employee.FullName,
                    h.PurchaseRequest.Employee.Department != null ? h.PurchaseRequest.Employee.Department.DepartmentName : null,
                    h.PurchaseRequest.Vendor, h.PurchaseRequest.Category,
                    h.PurchaseRequest.EstimatedAmount, h.PurchaseRequest.Currency, h.FromStatus.ToString(), h.ToStatus.ToString(),
                    h.ChangedByEmployeeId, h.Reason, h.ChangedAt))
                .ToListAsync(ct)
            : [];
        return claims.Concat(requests).OrderByDescending(h => h.ChangedAt).Take(take).ToList();
    }
}

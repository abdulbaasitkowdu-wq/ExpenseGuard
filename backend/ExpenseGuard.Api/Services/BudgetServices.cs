using ExpenseGuard.Api.Data;
using ExpenseGuard.Api.DTOs;
using ExpenseGuard.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ExpenseGuard.Api.Services;

public interface IDepartmentService
{
    Task<PagedResult<DepartmentDto>> ListAsync(int page, int pageSize, bool? active, CancellationToken ct);
    Task<DepartmentDto?> GetAsync(int id, CancellationToken ct);
    Task<DepartmentDto> CreateAsync(CreateDepartmentRequest request, CancellationToken ct);
    Task<DepartmentDto?> UpdateAsync(int id, UpdateDepartmentRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}

public interface IBudgetService
{
    Task<BudgetDto> AllocateAsync(AllocateBudgetRequest request, CancellationToken ct);
    Task<BudgetDto?> GetAsync(int id, CancellationToken ct);
    Task<AvailabilityDto?> CheckAvailabilityAsync(int id, decimal amount, CancellationToken ct);
    Task<BudgetDto?> ReserveAsync(int id, BudgetAmountRequest request, CancellationToken ct);
    Task<BudgetDto?> ReleaseAsync(int id, BudgetAmountRequest request, CancellationToken ct);
    Task<BudgetDto?> SpendAsync(int id, SpendBudgetRequest request, CancellationToken ct);
    Task<PagedResult<BudgetTransactionDto>?> TransactionsAsync(int id, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<BudgetAlertDto>?> AlertsAsync(int id, BudgetAlertStatus? status, CancellationToken ct);
    Task<PagedResult<UtilizationDto>> UtilizationAsync(int page, int pageSize, int? departmentId, CancellationToken ct);
}

public sealed class DepartmentService(AppDbContext db) : IDepartmentService
{
    public async Task<PagedResult<DepartmentDto>> ListAsync(int page, int pageSize, bool? active, CancellationToken ct)
    {
        var query = db.Departments.AsNoTracking().Where(d => !active.HasValue || d.IsActive == active);
        var count = await query.CountAsync(ct);
        var items = await query.OrderBy(d => d.DepartmentName).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(d => Map(d)).ToListAsync(ct);
        return new(items, page, pageSize, count);
    }

    public async Task<DepartmentDto?> GetAsync(int id, CancellationToken ct) =>
        await db.Departments.AsNoTracking().Where(d => d.DepartmentId == id).Select(d => Map(d)).SingleOrDefaultAsync(ct);

    public async Task<DepartmentDto> CreateAsync(CreateDepartmentRequest request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Departments.AnyAsync(d => d.Code == code, ct))
            throw new DuplicateResourceException("Department code already exists.");
        var entity = new Department { Code = code, DepartmentName = request.Name.Trim() };
        db.Departments.Add(entity);
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<DepartmentDto?> UpdateAsync(int id, UpdateDepartmentRequest request, CancellationToken ct)
    {
        var entity = await db.Departments.SingleOrDefaultAsync(d => d.DepartmentId == id, ct);
        if (entity is null) return null;
        EnsureVersion(entity.Version, request.Version);
        entity.DepartmentName = request.Name.Trim();
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.Version = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var entity = await db.Departments.SingleOrDefaultAsync(d => d.DepartmentId == id, ct);
        if (entity is null) return false;
        if (await db.Employees.AnyAsync(e => e.DepartmentId == id, ct) ||
            await db.Budgets.AnyAsync(b => b.DepartmentId == id, ct))
            throw new InvalidBudgetOperationException("Department with employees or budgets cannot be deleted.");
        db.Departments.Remove(entity);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static DepartmentDto Map(Department d) =>
        new(d.DepartmentId, d.Code, d.DepartmentName, d.IsActive, d.Version);

    private static void EnsureVersion(Guid actual, Guid supplied)
    {
        if (supplied == Guid.Empty || actual != supplied) throw new DbUpdateConcurrencyException("The resource was modified.");
    }
}

public sealed class BudgetService(AppDbContext db) : IBudgetService
{
    public const decimal MaxDepartmentBudget = 100_000_000m;

    public async Task<BudgetDto> AllocateAsync(AllocateBudgetRequest request, CancellationToken ct)
    {
        var department = await db.Departments.SingleOrDefaultAsync(d => d.DepartmentId == request.DepartmentId, ct)
            ?? throw new KeyNotFoundException("Department not found.");
        if (!department.IsActive) throw new InvalidBudgetOperationException("Department is inactive.");
        if (request.Amount > MaxDepartmentBudget)
            throw new InvalidBudgetOperationException($"A budget cannot exceed {MaxDepartmentBudget:N0}.");

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await db.BudgetTransactions.AsNoTracking()
                .Where(t => t.IdempotencyKey == request.IdempotencyKey && t.Type == BudgetTransactionType.Allocation)
                .Select(t => t.BudgetId).SingleOrDefaultAsync(ct);
            if (existing != 0) return Map(await db.Budgets.AsNoTracking().SingleAsync(b => b.BudgetId == existing, ct));
        }

        var overlaps = await db.Budgets.AnyAsync(b => b.DepartmentId == request.DepartmentId &&
            b.Currency == request.Currency && b.PeriodStart == request.PeriodStart && b.PeriodEnd == request.PeriodEnd, ct);
        if (overlaps) throw new DuplicateResourceException("A budget already exists for this department, period and currency.");

        var allocated = await db.Budgets.Where(b => b.DepartmentId == request.DepartmentId && b.Currency == request.Currency && b.IsActive)
            .SumAsync(b => b.AllocatedAmount, ct);
        if (allocated + request.Amount > MaxDepartmentBudget)
            throw new InvalidBudgetOperationException(
                $"Department total cannot exceed {MaxDepartmentBudget:N0} {request.Currency}. Remaining capacity is {Math.Max(0, MaxDepartmentBudget - allocated):N2}.");

        var budget = new Budget
        {
            DepartmentId = request.DepartmentId,
            Name = request.Name.Trim(),
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            Currency = request.Currency,
            AllocatedAmount = request.Amount
        };
        db.Budgets.Add(budget);
        AddTransaction(budget, BudgetTransactionType.Allocation, request.Amount, null, "Initial allocation", request.IdempotencyKey);
        await db.SaveChangesAsync(ct);
        return Map(budget);
    }

    public async Task<BudgetDto?> GetAsync(int id, CancellationToken ct) =>
        await db.Budgets.AsNoTracking().Where(b => b.BudgetId == id).Select(b => Map(b)).SingleOrDefaultAsync(ct);

    public async Task<AvailabilityDto?> CheckAvailabilityAsync(int id, decimal amount, CancellationToken ct)
    {
        if (amount <= 0) throw new InvalidBudgetOperationException("Amount must be positive.");
        var budget = await db.Budgets.AsNoTracking().SingleOrDefaultAsync(b => b.BudgetId == id, ct);
        return budget is null ? null : new(id, amount, budget.AvailableAmount, budget.IsActive && budget.AvailableAmount >= amount);
    }

    public Task<BudgetDto?> ReserveAsync(int id, BudgetAmountRequest request, CancellationToken ct) =>
        MutateAsync(id, request, BudgetTransactionType.Reservation, b => b.Reserve(request.Amount), ct);

    public Task<BudgetDto?> ReleaseAsync(int id, BudgetAmountRequest request, CancellationToken ct) =>
        MutateAsync(id, request, BudgetTransactionType.Release, b => b.Release(request.Amount), ct);

    public Task<BudgetDto?> SpendAsync(int id, SpendBudgetRequest request, CancellationToken ct) =>
        MutateAsync(id, request, BudgetTransactionType.Spend, b => b.Spend(request.Amount, request.FromReservation), ct);

    private async Task<BudgetDto?> MutateAsync(
        int id, BudgetAmountRequest request, BudgetTransactionType type, Action<Budget> mutation, CancellationToken ct)
    {
        IDbContextTransaction? transaction = null;
        if (db.Database.IsRelational()) transaction = await db.Database.BeginTransactionAsync(ct);
        await using var transactionScope = transaction;

        var prior = await db.BudgetTransactions.AsNoTracking()
            .SingleOrDefaultAsync(t => t.BudgetId == id && t.IdempotencyKey == request.IdempotencyKey, ct);
        if (prior is not null)
        {
            var replay = await db.Budgets.AsNoTracking().SingleAsync(b => b.BudgetId == id, ct);
            return Map(replay);
        }

        var budget = await db.Budgets.SingleOrDefaultAsync(b => b.BudgetId == id, ct);
        if (budget is null) return null;
        if (!budget.IsActive) throw new InvalidBudgetOperationException("Budget is inactive.");
        if (budget.Version != request.Version || request.Version == Guid.Empty)
            throw new DbUpdateConcurrencyException("The budget was modified.");

        mutation(budget);
        AddTransaction(budget, type, request.Amount, request.Reference, request.Description, request.IdempotencyKey);
        await UpdateAlertsAsync(budget, ct);
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Map(budget);
    }

    public async Task<PagedResult<BudgetTransactionDto>?> TransactionsAsync(
        int id, int page, int pageSize, CancellationToken ct)
    {
        if (!await db.Budgets.AnyAsync(b => b.BudgetId == id, ct)) return null;
        var query = db.BudgetTransactions.AsNoTracking().Where(t => t.BudgetId == id);
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.BudgetTransactionId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new BudgetTransactionDto(t.BudgetTransactionId, t.Type, t.Amount, t.AllocatedBalance,
                t.ReservedBalance, t.SpentBalance, t.Reference, t.Description, t.CreatedAt)).ToListAsync(ct);
        return new(items, page, pageSize, count);
    }

    public async Task<IReadOnlyList<BudgetAlertDto>?> AlertsAsync(
        int id, BudgetAlertStatus? status, CancellationToken ct)
    {
        if (!await db.Budgets.AnyAsync(b => b.BudgetId == id, ct)) return null;
        return await db.BudgetAlerts.AsNoTracking()
            .Where(a => a.BudgetId == id && (!status.HasValue || a.Status == status))
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new BudgetAlertDto(a.BudgetAlertId, a.ThresholdPercent, a.UtilizationPercent,
                a.Severity, a.Status, a.Message, a.CreatedAt)).ToListAsync(ct);
    }

    public async Task<PagedResult<UtilizationDto>> UtilizationAsync(
        int page, int pageSize, int? departmentId, CancellationToken ct)
    {
        var query = db.Budgets.AsNoTracking().Where(b => !departmentId.HasValue || b.DepartmentId == departmentId);
        var count = await query.CountAsync(ct);
        var items = await query.OrderBy(b => b.Department.DepartmentName).ThenBy(b => b.PeriodStart)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(b => new UtilizationDto(b.BudgetId, b.DepartmentId, b.Department.DepartmentName, b.Name, b.Currency,
                b.AllocatedAmount, b.ReservedAmount, b.SpentAmount,
                b.AllocatedAmount - b.ReservedAmount - b.SpentAmount,
                b.AllocatedAmount == 0 ? 0 : (b.ReservedAmount + b.SpentAmount) / b.AllocatedAmount * 100,
                b.Alerts.Count(a => a.Status == BudgetAlertStatus.Open))).ToListAsync(ct);
        return new(items, page, pageSize, count);
    }

    private async Task UpdateAlertsAsync(Budget budget, CancellationToken ct)
    {
        var utilization = budget.AllocatedAmount == 0 ? 0 : (budget.ReservedAmount + budget.SpentAmount) / budget.AllocatedAmount * 100;
        foreach (var threshold in new[] { 80m, 90m, 100m })
        {
            if (utilization < threshold ||
                await db.BudgetAlerts.AnyAsync(a => a.BudgetId == budget.BudgetId &&
                    a.ThresholdPercent == threshold && a.Status == BudgetAlertStatus.Open, ct)) continue;
            budget.Alerts.Add(new BudgetAlert
            {
                ThresholdPercent = threshold,
                UtilizationPercent = utilization,
                Severity = threshold >= 100 ? BudgetAlertSeverity.Critical : BudgetAlertSeverity.Warning,
                Message = $"Budget utilization reached {utilization:F2}%."
            });
        }
    }

    private static void AddTransaction(Budget budget, BudgetTransactionType type, decimal amount,
        string? reference, string? description, string? idempotencyKey) =>
        budget.Transactions.Add(new BudgetTransaction
        {
            Type = type, Amount = amount, AllocatedBalance = budget.AllocatedAmount,
            ReservedBalance = budget.ReservedAmount, SpentBalance = budget.SpentAmount,
            Reference = reference, Description = description, IdempotencyKey = idempotencyKey
        });

    private static BudgetDto Map(Budget b) =>
        new(b.BudgetId, b.DepartmentId, b.Name, b.PeriodStart, b.PeriodEnd, b.Currency,
            b.AllocatedAmount, b.ReservedAmount, b.SpentAmount, b.AvailableAmount, b.IsActive, b.Version);
}

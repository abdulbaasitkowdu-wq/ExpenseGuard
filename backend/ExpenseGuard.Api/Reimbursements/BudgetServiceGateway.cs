using ExpenseGuard.Api.Data;
using ExpenseGuard.Api.DTOs;
using ExpenseGuard.Api.Models;
using ExpenseGuard.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace ExpenseGuard.Api.Reimbursements;

public sealed class BudgetServiceGateway(AppDbContext db, IBudgetService budgets) : IBudgetGateway
{
    public async Task<BudgetCheckResult> CheckAsync(int departmentId, decimal amount, CancellationToken cancellationToken)
    {
        var budget = await FindAsync(departmentId, null, cancellationToken);
        if (budget is null) return new(false, "No active budget covers this department.");
        var availability = await budgets.CheckAvailabilityAsync(budget.BudgetId, amount, cancellationToken);
        return availability is null
            ? new(false, "Budget was not found.")
            : new(availability.IsAvailable, availability.IsAvailable ? null : "Insufficient available budget.");
    }

    public async Task RecordPaymentAsync(int departmentId, decimal amount, int reimbursementId, CancellationToken cancellationToken)
    {
        var budget = await FindAsync(departmentId, null, cancellationToken)
            ?? throw new InvalidOperationException("No active budget covers this department.");
        await budgets.SpendAsync(budget.BudgetId, new SpendBudgetRequest
        {
            Amount = amount,
            FromReservation = true,
            IdempotencyKey = $"reimbursement:{reimbursementId}:spend",
            Reference = $"reimbursement:{reimbursementId}",
            Description = "Reimbursement payment",
            Version = budget.Version
        }, cancellationToken);
    }

    public async Task ReleaseReservationAsync(int departmentId, decimal amount, int reimbursementId, CancellationToken cancellationToken)
    {
        var budget = await FindAsync(departmentId, null, cancellationToken);
        if (budget is null) return;
        await budgets.ReleaseAsync(budget.BudgetId, new BudgetAmountRequest
        {
            Amount = amount,
            IdempotencyKey = $"reimbursement:{reimbursementId}:release",
            Reference = $"reimbursement:{reimbursementId}",
            Description = "Released after rejection or revision",
            Version = budget.Version
        }, cancellationToken);
    }

    private Task<Budget?> FindAsync(int departmentId, string? currency, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = db.Budgets.Where(b => b.DepartmentId == departmentId && b.IsActive
            && b.PeriodStart <= today && b.PeriodEnd >= today);
        if (!string.IsNullOrWhiteSpace(currency))
            query = query.Where(b => b.Currency == currency);
        return query.OrderByDescending(b => b.AllocatedAmount - b.ReservedAmount - b.SpentAmount)
            .FirstOrDefaultAsync(ct);
    }
}

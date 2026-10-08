namespace ExpenseGuard.Api.Models;

public enum BudgetTransactionType
{
    Allocation,
    Reservation,
    Release,
    Spend
}

public class BudgetTransaction
{
    public long BudgetTransactionId { get; set; }
    public int BudgetId { get; set; }
    public BudgetTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public decimal AllocatedBalance { get; set; }
    public decimal ReservedBalance { get; set; }
    public decimal SpentBalance { get; set; }
    public string? Reference { get; set; }
    public string? Description { get; set; }
    public string? IdempotencyKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Budget Budget { get; set; } = null!;
}

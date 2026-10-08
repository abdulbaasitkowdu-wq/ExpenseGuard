namespace ExpenseGuard.Api.Models;

public class Budget
{
    public int BudgetId { get; set; }
    public int DepartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public string Currency { get; set; } = "LKR";
    public decimal AllocatedAmount { get; set; }
    public decimal ReservedAmount { get; set; }
    public decimal SpentAmount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();

    public Department Department { get; set; } = null!;
    public ICollection<BudgetTransaction> Transactions { get; set; } = [];
    public ICollection<BudgetAlert> Alerts { get; set; } = [];

    public decimal AvailableAmount => AllocatedAmount - ReservedAmount - SpentAmount;

    public void Allocate(decimal amount)
    {
        RequirePositive(amount);
        AllocatedAmount += amount;
        Touch();
    }

    public void Reserve(decimal amount)
    {
        RequirePositive(amount);
        if (AvailableAmount < amount) throw new InsufficientBudgetException();
        ReservedAmount += amount;
        Touch();
    }

    public void Release(decimal amount)
    {
        RequirePositive(amount);
        if (ReservedAmount < amount) throw new InvalidBudgetOperationException("Cannot release more than is reserved.");
        ReservedAmount -= amount;
        Touch();
    }

    public void Spend(decimal amount, bool fromReservation)
    {
        RequirePositive(amount);
        if (fromReservation)
        {
            if (ReservedAmount < amount) throw new InvalidBudgetOperationException("Reserved balance is insufficient.");
            ReservedAmount -= amount;
        }
        else if (AvailableAmount < amount)
        {
            throw new InsufficientBudgetException();
        }

        SpentAmount += amount;
        Touch();
    }

    private void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
        Version = Guid.NewGuid();
    }

    private static void RequirePositive(decimal amount)
    {
        if (amount <= 0) throw new InvalidBudgetOperationException("Amount must be positive.");
    }
}

public sealed class InsufficientBudgetException : Exception
{
    public InsufficientBudgetException() : base("Insufficient available budget.") { }
}

public sealed class InvalidBudgetOperationException(string message) : Exception(message);
public sealed class DuplicateResourceException(string message) : Exception(message);
namespace ExpenseGuard.Api.Models;

public enum BudgetAlertSeverity { Warning, Critical }
public enum BudgetAlertStatus { Open, Acknowledged, Resolved }

public class BudgetAlert
{
    public long BudgetAlertId { get; set; }
    public int BudgetId { get; set; }
    public decimal ThresholdPercent { get; set; }
    public decimal UtilizationPercent { get; set; }
    public BudgetAlertSeverity Severity { get; set; }
    public BudgetAlertStatus Status { get; set; } = BudgetAlertStatus.Open;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public Budget Budget { get; set; } = null!;
}

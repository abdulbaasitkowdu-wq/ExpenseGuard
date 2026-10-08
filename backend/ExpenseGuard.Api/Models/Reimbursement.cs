namespace ExpenseGuard.Api.Models;

public class Reimbursement
{
    public int ReimbursementId { get; set; }
    public int ExpenseClaimId { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "LKR";
    public string Status { get; set; } = ReimbursementStatuses.PendingApproval;
    public string? PaymentReference { get; set; }
    public string? PaymentProvider { get; set; }
    public Guid? PaymentId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public int RetryCount { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public ExpenseClaim ExpenseClaim { get; set; } = null!;
    public ICollection<PaymentTransaction> PaymentTransactions { get; set; } = [];
    public ICollection<ApprovalProcess> ApprovalProcesses { get; set; } = [];
}

public static class ReimbursementStatuses
{
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string RevisionRequired = "REVISION_REQUIRED";
    public const string Rejected = "REJECTED";
    public const string Approved = "APPROVED";
    public const string BudgetReviewRequired = "BUDGET_REVIEW_REQUIRED";
    public const string Processing = "PROCESSING";
    public const string PaymentPending = "PAYMENT_PENDING";
    public const string Paid = "PAID";
    public const string PaymentFailed = "PAYMENT_FAILED";
}
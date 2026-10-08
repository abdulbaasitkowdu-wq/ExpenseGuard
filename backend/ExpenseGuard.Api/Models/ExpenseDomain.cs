namespace ExpenseGuard.Api.Models;

public enum ClaimStatus { Draft, Submitted, UnderReview, Approved, Rejected, NeedsCorrection, Cancelled }
public enum ClaimFlow { OutOfPocket, PrePurchase }
public enum PurchaseRequestStatus { Draft, Submitted, Approved, Rejected, Cancelled }
public enum ReceiptProcessingStatus { Pending, Processed, NeedsReview, Failed }

public class Designation
{
    public int DesignationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<Employee> Employees { get; set; } = [];
}

public class PurchaseRequest
{
    public int PurchaseRequestId { get; set; }
    public int EmployeeId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal EstimatedAmount { get; set; }
    public string Currency { get; set; } = "LKR";
    public string? Vendor { get; set; }
    public string? Category { get; set; }
    public PurchaseRequestStatus Status { get; set; } = PurchaseRequestStatus.Draft;
    public DateTime? SubmittedAt { get; set; }
    public string? CurrentRequiredRole { get; set; }
    public string? ReviewJson { get; set; }
    public string? ApprovalJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public long Version { get; set; }
    public Employee Employee { get; set; } = null!;
    public ICollection<ExpenseClaim> Claims { get; set; } = [];
    public ICollection<PurchaseRequestStatusHistory> StatusHistory { get; set; } = [];
}

public class Receipt
{
    public int ReceiptId { get; set; }
    public int ExpenseClaimId { get; set; }
    public string StorageUrl { get; set; } = string.Empty;
    public string PublicId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public ReceiptProcessingStatus ProcessingStatus { get; set; } = ReceiptProcessingStatus.Pending;
    public string? ExtractedVendor { get; set; }
    public decimal? ExtractedAmount { get; set; }
    public DateTime? ExtractedDate { get; set; }
    public string? ExtractedCurrency { get; set; }
    public string? ExtractedText { get; set; }
    public decimal? Confidence { get; set; }
    public bool RequiresManualReview { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CorrectedAt { get; set; }
    public int? CorrectedByEmployeeId { get; set; }
    public ExpenseClaim ExpenseClaim { get; set; } = null!;
}

public class ClaimStatusHistory
{
    public int ClaimStatusHistoryId { get; set; }
    public int ExpenseClaimId { get; set; }
    public ClaimStatus FromStatus { get; set; }
    public ClaimStatus ToStatus { get; set; }
    public int ChangedByEmployeeId { get; set; }
    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public ExpenseClaim ExpenseClaim { get; set; } = null!;
}

public class PurchaseRequestStatusHistory
{
    public int PurchaseRequestStatusHistoryId { get; set; }
    public int PurchaseRequestId { get; set; }
    public PurchaseRequestStatus FromStatus { get; set; }
    public PurchaseRequestStatus ToStatus { get; set; }
    public int ChangedByEmployeeId { get; set; }
    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public PurchaseRequest PurchaseRequest { get; set; } = null!;
}

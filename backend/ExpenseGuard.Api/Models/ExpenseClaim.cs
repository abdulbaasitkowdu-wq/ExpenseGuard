namespace ExpenseGuard.Api.Models;

public class ExpenseClaim
{
    public int ExpenseClaimId { get; set; }
    public int EmployeeId { get; set; }
    public int? PurchaseRequestId { get; set; }
    public decimal Amount { get; set; }
    public string Category { get; set; } = string.Empty;
    public ClaimStatus Status { get; set; } = ClaimStatus.Draft;
    public ClaimFlow Flow { get; set; } = ClaimFlow.OutOfPocket;
    public string Description { get; set; } = string.Empty;
    public string Currency { get; set; } = "LKR";
    public string? Vendor { get; set; }
    public string PurchaseNo { get; set; } = string.Empty;
    public DateTime? PurchaseDate { get; set; }
    public string? ReceiptImg { get; set; }
    public string? ReceiptDoc { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
    public long Version { get; set; }

    public Employee Employee { get; set; } = null!;
    public PurchaseRequest? PurchaseRequest { get; set; }
    public ICollection<Receipt> Receipts { get; set; } = [];
    public ICollection<ClaimStatusHistory> StatusHistory { get; set; } = [];
    public ICollection<FraudFlag> FraudFlags { get; set; } = [];
    public ICollection<FraudEvaluation> FraudEvaluations { get; set; } = [];
    public ICollection<PolicyEvaluation> PolicyEvaluations { get; set; } = [];
    public Reimbursement? Reimbursement { get; set; }
}

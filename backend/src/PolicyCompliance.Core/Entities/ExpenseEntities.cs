using PolicyCompliance.Core.Enums;

namespace PolicyCompliance.Core.Entities;

public class ExpenseClaim
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public User? Employee { get; set; }
    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }

    public string ClaimNumber { get; set; } = string.Empty;
    public DateTime ClaimDate { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "LKR";
    public string MerchantName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public ClaimStatus Status { get; set; } = ClaimStatus.SUBMITTED;
    public PolicyStatus PolicyStatus { get; set; } = PolicyStatus.PENDING;
    public RiskStatus RiskStatus { get; set; } = RiskStatus.NOT_ASSESSED;

    public string? LatestRevisionComment { get; set; }
    public string? LatestRejectionReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ExpenseItem> Items { get; set; } = new List<ExpenseItem>();
    public ICollection<PolicyViolation> Violations { get; set; } = new List<PolicyViolation>();
    public ICollection<RiskAssessment> RiskAssessments { get; set; } = new List<RiskAssessment>();
    public ICollection<DuplicateMatch> DuplicateMatches { get; set; } = new List<DuplicateMatch>();
    public ICollection<ManagerReview> Reviews { get; set; } = new List<ManagerReview>();
    public ICollection<ComplianceCheck> ComplianceChecks { get; set; } = new List<ComplianceCheck>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}

public class ExpenseItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExpenseClaimId { get; set; }
    public ExpenseClaim? ExpenseClaim { get; set; }

    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow;
    public string Category { get; set; } = string.Empty;
    public string Merchant { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "LKR";
    public string Description { get; set; } = string.Empty;
    public string? ReceiptUrl { get; set; }
}

public class ExpensePolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PolicyName { get; set; } = string.Empty;
    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public bool IsGlobalPolicy { get; set; } = true;
    public string Category { get; set; } = string.Empty;
    public decimal MaximumAmount { get; set; }
    public bool RequiresReceipt { get; set; } = true;
    public bool RequiresManagerApproval { get; set; } = true;
    public string AllowedCurrency { get; set; } = "LKR";
    public bool Active { get; set; } = true;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.AddYears(-1);
    public DateTime? EffectiveTo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

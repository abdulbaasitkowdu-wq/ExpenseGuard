namespace ExpenseGuard.Api.Models;

public class PolicyDesignation
{
    public int PolicyDesignationId { get; set; }
    public int PolicyId { get; set; }
    public string Designation { get; set; } = string.Empty;
    public Policy Policy { get; set; } = null!;
}

public class PolicyEvaluation
{
    public int PolicyEvaluationId { get; set; }
    public int ExpenseClaimId { get; set; }
    public int? PolicyId { get; set; }
    public string InputFingerprint { get; set; } = string.Empty;
    public string Outcome { get; set; } = "compliant";
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    public ExpenseClaim ExpenseClaim { get; set; } = null!;
    public Policy? Policy { get; set; }
    public ICollection<PolicyViolation> Violations { get; set; } = [];
}

public class PolicyViolation
{
    public int PolicyViolationId { get; set; }
    public int PolicyEvaluationId { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "error";
    public decimal? ExpectedAmount { get; set; }
    public decimal? ActualAmount { get; set; }
    public PolicyEvaluation PolicyEvaluation { get; set; } = null!;
}

public class FraudEvaluation
{
    public int FraudEvaluationId { get; set; }
    public int ExpenseClaimId { get; set; }
    public string InputFingerprint { get; set; } = string.Empty;
    public string? NormalizedInvoiceNumber { get; set; }
    public decimal ClaimAmount { get; set; }
    public decimal? ReceiptAmount { get; set; }
    public decimal RiskScore { get; set; }
    public string RiskLevel { get; set; } = "low";
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    public ExpenseClaim ExpenseClaim { get; set; } = null!;
    public ICollection<FraudFlag> Flags { get; set; } = [];
}

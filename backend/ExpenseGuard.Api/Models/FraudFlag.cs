namespace ExpenseGuard.Api.Models;

public class FraudFlag
{
    public int FraudFlagId { get; set; }
    public int ExpenseClaimId { get; set; }
    public int? FraudEvaluationId { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string Severity { get; set; } = "medium";
    public string Source { get; set; } = "deterministic";
    public string FlagReason { get; set; } = string.Empty;
    public string EvidenceJson { get; set; } = "{}";
    public decimal RiskScore { get; set; }
    public string Status { get; set; } = "open";
    public string? ResolutionNote { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ExpenseClaim ExpenseClaim { get; set; } = null!;
    public FraudEvaluation? FraudEvaluation { get; set; }
}

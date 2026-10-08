using PolicyCompliance.Core.Enums;

namespace PolicyCompliance.Core.Entities;

public class PolicyViolation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExpenseClaimId { get; set; }
    public ExpenseClaim? ExpenseClaim { get; set; }

    public Guid? PolicyId { get; set; }
    public ExpensePolicy? Policy { get; set; }

    public string RuleCode { get; set; } = string.Empty;
    public ViolationSeverity Severity { get; set; } = ViolationSeverity.HIGH;
    public string Message { get; set; } = string.Empty;
    public string ActualValue { get; set; } = string.Empty;
    public string AllowedValue { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class RiskAssessment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExpenseClaimId { get; set; }
    public ExpenseClaim? ExpenseClaim { get; set; }

    public int RiskScore { get; set; } // 0 - 100
    public RiskLevel RiskLevel { get; set; } = RiskLevel.LOW_RISK;

    public bool DuplicateDetected { get; set; }
    public bool UnusualAmountDetected { get; set; }
    public bool SuspiciousPatternDetected { get; set; }

    public string ReasonSummary { get; set; } = string.Empty;
    public string SignalsJson { get; set; } = "[]"; // Serialized structured signals
    public string AgentVersion { get; set; } = "v1.2-deterministic-hybrid";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class DuplicateMatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExpenseClaimId { get; set; }
    public ExpenseClaim? ExpenseClaim { get; set; }

    public Guid MatchingClaimId { get; set; }
    public ExpenseClaim? MatchingClaim { get; set; }

    public decimal SimilarityScore { get; set; } // 0.00 to 1.00
    public DuplicateMatchType MatchType { get; set; } = DuplicateMatchType.EXACT;
    public string Evidence { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ManagerReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExpenseClaimId { get; set; }
    public ExpenseClaim? ExpenseClaim { get; set; }

    public Guid ManagerId { get; set; }
    public User? Manager { get; set; }

    public ReviewDecision Decision { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;
}

public class ComplianceCheck
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExpenseClaimId { get; set; }
    public ExpenseClaim? ExpenseClaim { get; set; }

    public CheckType CheckType { get; set; }
    public CheckResult Result { get; set; }
    public string Details { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = "ExpenseClaim";
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid? ActorUserId { get; set; }
    public string ActorRole { get; set; } = "System";
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public ExpenseClaim? ExpenseClaim { get; set; }
}

public class AgentExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string WorkflowId { get; set; } = Guid.NewGuid().ToString();
    public string AgentName { get; set; } = "FraudAnomalyRiskAgent";
    public string InputReference { get; set; } = string.Empty;
    public string OutputReference { get; set; } = string.Empty;
    public AgentExecutionStatus Status { get; set; } = AgentExecutionStatus.STARTED;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

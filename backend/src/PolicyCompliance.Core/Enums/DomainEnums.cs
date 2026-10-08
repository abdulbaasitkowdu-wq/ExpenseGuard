namespace PolicyCompliance.Core.Enums;

public enum UserRole
{
    Employee,
    Manager,
    Finance,
    Admin
}

public enum ClaimStatus
{
    SUBMITTED,
    POLICY_VALIDATING,
    RISK_ASSESSING,
    WAITING_FOR_MANAGER_APPROVAL,
    APPROVED,
    REJECTED,
    REVISION_REQUIRED,
    REIMBURSED
}

public enum PolicyStatus
{
    PENDING,
    COMPLIANT,
    VIOLATIONS_FOUND
}

public enum RiskLevel
{
    LOW_RISK,
    MEDIUM_RISK,
    HIGH_RISK,
    REVIEW_REQUIRED
}

public enum RiskStatus
{
    NOT_ASSESSED,
    LOW_RISK,
    MEDIUM_RISK,
    HIGH_RISK,
    REVIEW_REQUIRED
}

public enum ViolationSeverity
{
    INFO,
    WARNING,
    HIGH,
    CRITICAL
}

public enum ReviewDecision
{
    APPROVE,
    REJECT,
    REQUEST_REVISION
}

public enum CheckType
{
    POLICY_RULES,
    DUPLICATE_DETECTION,
    STATISTICAL_ANOMALY,
    AGENTIC_RISK
}

public enum CheckResult
{
    PASSED,
    WARNING,
    FAILED
}

public enum DuplicateMatchType
{
    EXACT,
    PARTIAL,
    POTENTIAL
}

public enum AgentExecutionStatus
{
    STARTED,
    SUCCESS,
    FAILED,
    SAFE_FALLBACK
}

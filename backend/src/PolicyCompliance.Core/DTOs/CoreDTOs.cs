using PolicyCompliance.Core.Enums;

namespace PolicyCompliance.Core.DTOs;

public class LoginRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
}

public class CreateExpenseItemDto
{
    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow;
    public string Category { get; set; } = string.Empty;
    public string Merchant { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "LKR";
    public string Description { get; set; } = string.Empty;
    public string? ReceiptUrl { get; set; }
}

public class CreateExpenseClaimDto
{
    public DateTime ClaimDate { get; set; } = DateTime.UtcNow;
    public string MerchantName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "LKR";
    public string Description { get; set; } = string.Empty;
    public List<CreateExpenseItemDto> Items { get; set; } = new();
}

public class ResubmitExpenseClaimDto
{
    public decimal TotalAmount { get; set; }
    public string MerchantName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ResubmissionNotes { get; set; } = string.Empty;
    public List<CreateExpenseItemDto> Items { get; set; } = new();
}

public class ExpenseItemDto
{
    public Guid Id { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Merchant { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ReceiptUrl { get; set; }
}

public class PolicyViolationDto
{
    public Guid Id { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ActualValue { get; set; } = string.Empty;
    public string AllowedValue { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class RiskSignalDto
{
    public string Type { get; set; } = string.Empty; // DUPLICATE, UNUSUAL_AMOUNT, SUSPICIOUS_PATTERN, THRESHOLD_EVASION
    public string Severity { get; set; } = string.Empty; // LOW, MEDIUM, HIGH, CRITICAL
    public string Evidence { get; set; } = string.Empty;
}

public class RiskAssessmentDto
{
    public Guid Id { get; set; }
    public int RiskScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public bool DuplicateDetected { get; set; }
    public bool UnusualAmountDetected { get; set; }
    public bool SuspiciousPatternDetected { get; set; }
    public string ReasonSummary { get; set; } = string.Empty;
    public List<RiskSignalDto> Signals { get; set; } = new();
    public string AgentVersion { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class DuplicateMatchDto
{
    public Guid Id { get; set; }
    public Guid MatchingClaimId { get; set; }
    public string MatchingClaimNumber { get; set; } = string.Empty;
    public decimal SimilarityScore { get; set; }
    public string MatchType { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public decimal MatchingAmount { get; set; }
    public DateTime MatchingClaimDate { get; set; }
    public string MatchingMerchant { get; set; } = string.Empty;
}

public class ManagerReviewDto
{
    public Guid Id { get; set; }
    public Guid ManagerId { get; set; }
    public string ManagerName { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public DateTime ReviewedAt { get; set; }
}

public class AuditLogDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid? ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string ActorRole { get; set; } = string.Empty;
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class ComplianceCheckDto
{
    public Guid Id { get; set; }
    public string CheckType { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; }
}

public class ExpenseClaimListDto
{
    public Guid Id { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public DateTime ClaimDate { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string MerchantName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PolicyStatus { get; set; } = string.Empty;
    public string RiskStatus { get; set; } = string.Empty;
    public int? RiskScore { get; set; }
    public int ViolationCount { get; set; }
    public int DuplicateCount { get; set; }
}

public class ExpenseClaimDetailDto : ExpenseClaimListDto
{
    public string Description { get; set; } = string.Empty;
    public string? LatestRevisionComment { get; set; }
    public string? LatestRejectionReason { get; set; }
    public List<ExpenseItemDto> Items { get; set; } = new();
    public List<PolicyViolationDto> Violations { get; set; } = new();
    public RiskAssessmentDto? RiskAssessment { get; set; }
    public List<DuplicateMatchDto> DuplicateMatches { get; set; } = new();
    public List<ManagerReviewDto> Reviews { get; set; } = new();
    public List<ComplianceCheckDto> ComplianceChecks { get; set; } = new();
    public List<AuditLogDto> AuditLogs { get; set; } = new();
}

public class ReviewQueueFilterDto
{
    public string? RiskLevel { get; set; }
    public string? PolicyStatus { get; set; }
    public string? Status { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? Category { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public string? SearchTerm { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class ManagerActionDto
{
    public string Comment { get; set; } = string.Empty;
}

public class ComplianceDashboardStatsDto
{
    public int TotalPendingReviews { get; set; }
    public int HighRiskClaims { get; set; }
    public int PolicyViolationsCount { get; set; }
    public int AwaitingRevisionCount { get; set; }
    public int TotalApproved { get; set; }
    public int TotalRejected { get; set; }
    public decimal TotalPendingAmount { get; set; }
    public List<ExpenseClaimListDto> RecentHighRiskClaims { get; set; } = new();
    public List<AuditLogDto> RecentActivities { get; set; } = new();
}

public class ExpensePolicyDto
{
    public Guid Id { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public bool IsGlobalPolicy { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal MaximumAmount { get; set; }
    public bool RequiresReceipt { get; set; }
    public bool RequiresManagerApproval { get; set; }
    public string AllowedCurrency { get; set; } = "LKR";
    public bool Active { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public class CreateExpensePolicyDto
{
    public string PolicyName { get; set; } = string.Empty;
    public Guid? DepartmentId { get; set; }
    public bool IsGlobalPolicy { get; set; } = true;
    public string Category { get; set; } = string.Empty;
    public decimal MaximumAmount { get; set; }
    public bool RequiresReceipt { get; set; } = true;
    public bool RequiresManagerApproval { get; set; } = true;
    public string AllowedCurrency { get; set; } = "LKR";
    public bool Active { get; set; } = true;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
}

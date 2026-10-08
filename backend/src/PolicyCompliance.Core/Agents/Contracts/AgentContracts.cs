using System.Text.Json.Serialization;

namespace PolicyCompliance.Core.Agents.Contracts;

public class AgentClaimInputContract
{
    [JsonPropertyName("claimId")]
    public string ClaimId { get; set; } = string.Empty;

    [JsonPropertyName("employeeId")]
    public string EmployeeId { get; set; } = string.Empty;

    [JsonPropertyName("departmentId")]
    public string DepartmentId { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "LKR";

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("merchant")]
    public string Merchant { get; set; } = string.Empty;

    [JsonPropertyName("expenseDate")]
    public string ExpenseDate { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("historicalClaims")]
    public List<AgentClaimHistoryItem> HistoricalClaims { get; set; } = new();

    [JsonPropertyName("departmentStatistics")]
    public List<AgentDepartmentCategoryStats> DepartmentStatistics { get; set; } = new();

    [JsonPropertyName("possibleDuplicates")]
    public List<AgentDuplicateCandidate> PossibleDuplicates { get; set; } = new();
}

public class AgentClaimHistoryItem
{
    [JsonPropertyName("claimId")]
    public string ClaimId { get; set; } = string.Empty;

    [JsonPropertyName("claimDate")]
    public string ClaimDate { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("merchant")]
    public string Merchant { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

public class AgentDepartmentCategoryStats
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("claimCount")]
    public int ClaimCount { get; set; }

    [JsonPropertyName("averageAmount")]
    public decimal AverageAmount { get; set; }

    [JsonPropertyName("medianAmount")]
    public decimal MedianAmount { get; set; }

    [JsonPropertyName("standardDeviation")]
    public decimal StandardDeviation { get; set; }

    [JsonPropertyName("p90Amount")]
    public decimal P90Amount { get; set; }
}

public class AgentDuplicateCandidate
{
    [JsonPropertyName("matchingClaimId")]
    public string MatchingClaimId { get; set; } = string.Empty;

    [JsonPropertyName("matchingClaimNumber")]
    public string MatchingClaimNumber { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("claimDate")]
    public string ClaimDate { get; set; } = string.Empty;

    [JsonPropertyName("merchant")]
    public string Merchant { get; set; } = string.Empty;

    [JsonPropertyName("similarityScore")]
    public decimal SimilarityScore { get; set; }

    [JsonPropertyName("matchType")]
    public string MatchType { get; set; } = "EXACT";

    [JsonPropertyName("evidence")]
    public string Evidence { get; set; } = string.Empty;
}

public class AgentRiskSignal
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // DUPLICATE, UNUSUAL_AMOUNT, SUSPICIOUS_PATTERN, THRESHOLD_EVASION

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty; // LOW, MEDIUM, HIGH, CRITICAL

    [JsonPropertyName("evidence")]
    public string Evidence { get; set; } = string.Empty;
}

public class AgentRiskAssessmentOutput
{
    [JsonPropertyName("riskLevel")]
    public string RiskLevel { get; set; } = "LOW_RISK"; // LOW_RISK, MEDIUM_RISK, HIGH_RISK, REVIEW_REQUIRED

    [JsonPropertyName("riskScore")]
    public int RiskScore { get; set; } // 0 - 100

    [JsonPropertyName("duplicateDetected")]
    public bool DuplicateDetected { get; set; }

    [JsonPropertyName("unusualAmountDetected")]
    public bool UnusualAmountDetected { get; set; }

    [JsonPropertyName("suspiciousPatternDetected")]
    public bool SuspiciousPatternDetected { get; set; }

    [JsonPropertyName("reasonSummary")]
    public string ReasonSummary { get; set; } = string.Empty;

    [JsonPropertyName("signals")]
    public List<AgentRiskSignal> Signals { get; set; } = new();

    [JsonPropertyName("agentVersion")]
    public string AgentVersion { get; set; } = "FraudAnomalyRiskAgent-v1.2";
}

using System.Text.Json;
using PolicyCompliance.Core.Agents.Contracts;
using PolicyCompliance.Core.Entities;
using PolicyCompliance.Core.Enums;
using PolicyCompliance.Core.Tools;

namespace PolicyCompliance.Core.Agents.FraudAnomalyRiskAgent;

public interface IFraudAnomalyRiskAgent
{
    Task<AgentRiskAssessmentOutput> AnalyzeClaimAsync(Guid claimId, CancellationToken cancellationToken = default);
}

public class FraudAnomalyRiskAgent : IFraudAnomalyRiskAgent
{
    private readonly IComplianceAgentTools _tools;

    public FraudAnomalyRiskAgent(IComplianceAgentTools tools)
    {
        _tools = tools;
    }

    public async Task<AgentRiskAssessmentOutput> AnalyzeClaimAsync(Guid claimId, CancellationToken cancellationToken = default)
    {
        var output = new AgentRiskAssessmentOutput
        {
            AgentVersion = "FraudAnomalyRiskAgent-v1.2",
            RiskScore = 10,
            RiskLevel = "LOW_RISK"
        };

        try
        {
            // 1. Tool Call: Get Claim Details
            var claim = await _tools.GetClaimDetailsAsync(claimId, cancellationToken);
            if (claim == null)
            {
                return CreateSafeFallback("Claim record could not be retrieved from database for analysis.");
            }

            // 2. Tool Call: Get Employee Expense History
            var history = await _tools.GetEmployeeExpenseHistoryAsync(claim.EmployeeId, cancellationToken);

            // 3. Tool Call: Get Department Spending Statistics
            var deptStats = await _tools.GetDepartmentSpendingStatisticsAsync(claim.DepartmentId, claim.Category, cancellationToken);

            // 4. Tool Call: Find Possible Duplicates
            var duplicateCandidates = await _tools.FindPossibleDuplicateClaimsAsync(claimId, cancellationToken);

            int calculatedScore = 5;
            var signals = new List<AgentRiskSignal>();
            var summaryReasons = new List<string>();

            // --- ANALYSIS 1: DUPLICATE DETECTION ---
            if (duplicateCandidates != null && duplicateCandidates.Any())
            {
                var exactMatches = duplicateCandidates.Where(d => d.MatchType == "EXACT").ToList();
                var partialMatches = duplicateCandidates.Where(d => d.MatchType != "EXACT").ToList();

                if (exactMatches.Any())
                {
                    output.DuplicateDetected = true;
                    calculatedScore += 50;
                    var match = exactMatches.First();
                    signals.Add(new AgentRiskSignal
                    {
                        Type = "DUPLICATE",
                        Severity = "CRITICAL",
                        Evidence = $"Exact duplicate detected against Claim #{match.MatchingClaimNumber}. Matching parameters: Employee, Merchant ({match.Merchant}), Date ({match.ClaimDate}), and Amount ({match.Amount:N2})."
                    });
                    summaryReasons.Add("Exact duplicate claim identified");
                }
                else if (partialMatches.Any())
                {
                    output.DuplicateDetected = true;
                    calculatedScore += 25;
                    var topMatch = partialMatches.OrderByDescending(p => p.SimilarityScore).First();
                    signals.Add(new AgentRiskSignal
                    {
                        Type = "DUPLICATE",
                        Severity = "HIGH",
                        Evidence = $"High similarity duplicate candidate found (#{topMatch.MatchingClaimNumber}, Score: {topMatch.SimilarityScore:P0}). Evidence: {topMatch.Evidence}"
                    });
                    summaryReasons.Add("Potential duplicate claim identified");
                }
            }

            // --- ANALYSIS 2: STATISTICAL UNUSUAL AMOUNT DETECTION ---
            var categoryHistory = history?.Where(h => h.Category.Equals(claim.Category, StringComparison.OrdinalIgnoreCase) && h.ClaimId != claim.Id.ToString()).ToList() ?? new();
            if (categoryHistory.Any())
            {
                var historicalAmounts = categoryHistory.Select(h => (double)h.Amount).ToList();
                double employeeAvg = historicalAmounts.Average();
                double variance = historicalAmounts.Select(val => Math.Pow(val - employeeAvg, 2)).Average();
                double stdDev = Math.Sqrt(variance);

                // Z-score comparison if multiple claims exist
                if (historicalAmounts.Count >= 2 && stdDev > 0)
                {
                    double zScore = ((double)claim.TotalAmount - employeeAvg) / stdDev;
                    if (zScore >= 2.5)
                    {
                        output.UnusualAmountDetected = true;
                        calculatedScore += 45;
                        signals.Add(new AgentRiskSignal
                        {
                            Type = "UNUSUAL_AMOUNT",
                            Severity = "HIGH",
                            Evidence = $"Claim amount ({claim.TotalAmount:N2} {claim.Currency}) is an extreme statistical outlier (Z-Score: +{zScore:F1}σ). Employee category average is {employeeAvg:N2} (StdDev: ±{stdDev:N2})."
                        });
                        summaryReasons.Add("Statistical amount outlier against employee spending baseline");
                    }
                    else if (zScore >= 1.8)
                    {
                        output.UnusualAmountDetected = true;
                        calculatedScore += 25;
                        signals.Add(new AgentRiskSignal
                        {
                            Type = "UNUSUAL_AMOUNT",
                            Severity = "MEDIUM",
                            Evidence = $"Claim amount ({claim.TotalAmount:N2} {claim.Currency}) is notably higher than typical employee category spending (Z-Score: +{zScore:F1}σ, Average: {employeeAvg:N2})."
                        });
                        summaryReasons.Add("Elevated amount relative to employee category history");
                    }
                }
                else if (claim.TotalAmount > (decimal)(employeeAvg * 2.5) && claim.TotalAmount > 10000m)
                {
                    output.UnusualAmountDetected = true;
                    calculatedScore += 35;
                    signals.Add(new AgentRiskSignal
                    {
                        Type = "UNUSUAL_AMOUNT",
                        Severity = "HIGH",
                        Evidence = $"Claim amount ({claim.TotalAmount:N2} {claim.Currency}) is 2.5x higher than historical average of {employeeAvg:N2} {claim.Currency}."
                    });
                    summaryReasons.Add("Disproportionately high amount compared to employee average");
                }
            }

            // Department baseline comparison
            if (deptStats != null && deptStats.ClaimCount >= 3)
            {
                if (claim.TotalAmount > deptStats.P90Amount && claim.TotalAmount > 25000m)
                {
                    calculatedScore += 15;
                    output.UnusualAmountDetected = true;
                    signals.Add(new AgentRiskSignal
                    {
                        Type = "UNUSUAL_AMOUNT",
                        Severity = "MEDIUM",
                        Evidence = $"Claim amount exceeds the 90th percentile ({deptStats.P90Amount:N2}) of department spending for category '{claim.Category}'."
                    });
                    summaryReasons.Add("Exceeds department 90th percentile threshold");
                }
            }

            // --- ANALYSIS 3: SUSPICIOUS PATTERN DETECTION ---
            // Pattern A: Threshold Proximity Evasion (e.g. claim just below 50,000 / approval threshold)
            decimal thresholdCheck = 50000m;
            if (claim.TotalAmount >= 47500m && claim.TotalAmount < thresholdCheck)
            {
                output.SuspiciousPatternDetected = true;
                calculatedScore += 20;
                signals.Add(new AgentRiskSignal
                {
                    Type = "THRESHOLD_EVASION",
                    Severity = "MEDIUM",
                    Evidence = $"Claim amount ({claim.TotalAmount:N2}) is just 1-5% below standard senior management escalation threshold ({thresholdCheck:N2})."
                });
                summaryReasons.Add("Potential approval threshold evasion pattern");
            }

            // Pattern B: Merchant Velocity / Frequency clustering
            if (history != null && history.Any())
            {
                var recentMerchantClaims = history
                    .Where(h => h.Merchant.Equals(claim.MerchantName, StringComparison.OrdinalIgnoreCase) && h.ClaimId != claim.Id.ToString())
                    .ToList();

                if (recentMerchantClaims.Count >= 3)
                {
                    output.SuspiciousPatternDetected = true;
                    calculatedScore += 15;
                    signals.Add(new AgentRiskSignal
                    {
                        Type = "SUSPICIOUS_PATTERN",
                        Severity = "MEDIUM",
                        Evidence = $"High velocity of claims ({recentMerchantClaims.Count + 1} claims) submitted to merchant '{claim.MerchantName}'."
                    });
                    summaryReasons.Add($"Elevated claim velocity at '{claim.MerchantName}'");
                }
            }

            // Cap risk score between 5 and 100
            calculatedScore = Math.Clamp(calculatedScore, 5, 100);
            output.RiskScore = calculatedScore;
            output.Signals = signals;

            if (calculatedScore >= 80 || output.DuplicateDetected)
            {
                output.RiskLevel = "REVIEW_REQUIRED";
            }
            else if (calculatedScore >= 60)
            {
                output.RiskLevel = "HIGH_RISK";
            }
            else if (calculatedScore >= 35)
            {
                output.RiskLevel = "MEDIUM_RISK";
            }
            else
            {
                output.RiskLevel = "LOW_RISK";
            }

            output.ReasonSummary = summaryReasons.Any()
                ? string.Join("; ", summaryReasons)
                : "No significant fraud or anomaly risk indicators detected. Claim conforms to normal baseline patterns.";

            // 5. Tool Call: Persist structured RiskAssessment
            await _tools.CreateRiskAssessmentAsync(claimId, output, cancellationToken);

            // 6. Tool Call: Persist Risk Logs for anomalies
            foreach (var sig in signals)
            {
                await _tools.CreateRiskLogAsync(claimId, $"{sig.Type} ({sig.Severity}): {sig.Evidence}", sig.Severity, cancellationToken);
            }

            return output;
        }
        catch (Exception ex)
        {
            // SAFE FAILURE GUARANTEE: Agent never auto-approves on error; triggers human review fallback
            return CreateSafeFallback($"Agent anomaly analysis encountered an error: {ex.Message}. Mandating human managerial review.");
        }
    }

    private AgentRiskAssessmentOutput CreateSafeFallback(string reason)
    {
        return new AgentRiskAssessmentOutput
        {
            AgentVersion = "FraudAnomalyRiskAgent-v1.2-SafeFallback",
            RiskScore = 85,
            RiskLevel = "REVIEW_REQUIRED",
            DuplicateDetected = false,
            UnusualAmountDetected = false,
            SuspiciousPatternDetected = false,
            ReasonSummary = $"SAFE-FAILURE PAUSE: {reason}",
            Signals = new List<AgentRiskSignal>
            {
                new()
                {
                    Type = "SAFE_FALLBACK",
                    Severity = "HIGH",
                    Evidence = reason
                }
            }
        };
    }
}

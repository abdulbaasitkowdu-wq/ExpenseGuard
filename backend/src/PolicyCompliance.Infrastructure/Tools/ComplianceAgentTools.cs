using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PolicyCompliance.Core.Agents.Contracts;
using PolicyCompliance.Core.Entities;
using PolicyCompliance.Core.Enums;
using PolicyCompliance.Core.Services;
using PolicyCompliance.Core.Tools;
using PolicyCompliance.Infrastructure.Data;

namespace PolicyCompliance.Infrastructure.Tools;

public class ComplianceAgentTools : IComplianceAgentTools
{
    private readonly AppDbContext _context;
    private readonly IDuplicateDetectionService _duplicateDetector;

    public ComplianceAgentTools(AppDbContext context, IDuplicateDetectionService duplicateDetector)
    {
        _context = context;
        _duplicateDetector = duplicateDetector;
    }

    public async Task<List<AgentClaimHistoryItem>> GetEmployeeExpenseHistoryAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        // Controlled, read-only projection
        var claims = await _context.ExpenseClaims
            .AsNoTracking()
            .Where(c => c.EmployeeId == employeeId)
            .OrderByDescending(c => c.ClaimDate)
            .Take(50)
            .Select(c => new AgentClaimHistoryItem
            {
                ClaimId = c.Id.ToString(),
                ClaimDate = c.ClaimDate.ToString("yyyy-MM-dd"),
                Category = c.Category,
                Merchant = c.MerchantName,
                Amount = c.TotalAmount,
                Status = c.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return claims;
    }

    public async Task<AgentDepartmentCategoryStats> GetDepartmentSpendingStatisticsAsync(Guid departmentId, string category, CancellationToken cancellationToken = default)
    {
        var categoryClaims = await _context.ExpenseClaims
            .AsNoTracking()
            .Where(c => c.DepartmentId == departmentId && c.Category.ToLower() == category.ToLower() && c.Status != ClaimStatus.REJECTED)
            .Select(c => (double)c.TotalAmount)
            .ToListAsync(cancellationToken);

        if (!categoryClaims.Any())
        {
            return new AgentDepartmentCategoryStats
            {
                Category = category,
                ClaimCount = 0,
                AverageAmount = 0,
                MedianAmount = 0,
                StandardDeviation = 0,
                P90Amount = 0
            };
        }

        categoryClaims.Sort();
        int count = categoryClaims.Count;
        double avg = categoryClaims.Average();
        double variance = categoryClaims.Select(val => Math.Pow(val - avg, 2)).Average();
        double stdDev = Math.Sqrt(variance);

        // Median
        double median = count % 2 == 0
            ? (categoryClaims[count / 2 - 1] + categoryClaims[count / 2]) / 2.0
            : categoryClaims[count / 2];

        // 90th percentile
        int p90Index = (int)Math.Ceiling(0.90 * count) - 1;
        p90Index = Math.Clamp(p90Index, 0, count - 1);
        double p90 = categoryClaims[p90Index];

        return new AgentDepartmentCategoryStats
        {
            Category = category,
            ClaimCount = count,
            AverageAmount = (decimal)avg,
            MedianAmount = (decimal)median,
            StandardDeviation = (decimal)stdDev,
            P90Amount = (decimal)p90
        };
    }

    public async Task<List<AgentDuplicateCandidate>> FindPossibleDuplicateClaimsAsync(Guid claimId, CancellationToken cancellationToken = default)
    {
        var targetClaim = await _context.ExpenseClaims
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken);

        if (targetClaim == null)
            return new List<AgentDuplicateCandidate>();

        // Load candidate claims (e.g. within same employee or same department or matching amount/merchant)
        var candidates = await _context.ExpenseClaims
            .AsNoTracking()
            .Include(c => c.Items)
            .Where(c => c.Id != claimId && c.Status != ClaimStatus.REJECTED)
            .ToListAsync(cancellationToken);

        var matchResults = _duplicateDetector.DetectDuplicates(targetClaim, candidates);

        // Synchronize DuplicateMatch records in database
        var existingMatches = await _context.DuplicateMatches
            .Where(d => d.ExpenseClaimId == claimId)
            .ToListAsync(cancellationToken);

        if (existingMatches.Any())
        {
            _context.DuplicateMatches.RemoveRange(existingMatches);
        }

        foreach (var m in matchResults)
        {
            _context.DuplicateMatches.Add(new DuplicateMatch
            {
                ExpenseClaimId = claimId,
                MatchingClaimId = m.MatchingClaimId,
                SimilarityScore = m.SimilarityScore,
                MatchType = m.MatchType,
                Evidence = m.Evidence,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        return matchResults.Select(m => new AgentDuplicateCandidate
        {
            MatchingClaimId = m.MatchingClaimId.ToString(),
            MatchingClaimNumber = m.MatchingClaimNumber,
            Amount = m.MatchingAmount,
            ClaimDate = m.MatchingClaimDate.ToString("yyyy-MM-dd"),
            Merchant = m.MatchingMerchant,
            SimilarityScore = m.SimilarityScore,
            MatchType = m.MatchType.ToString(),
            Evidence = m.Evidence
        }).ToList();
    }

    public async Task<ExpenseClaim?> GetClaimDetailsAsync(Guid claimId, CancellationToken cancellationToken = default)
    {
        return await _context.ExpenseClaims
            .AsNoTracking()
            .Include(c => c.Items)
            .Include(c => c.Violations)
            .Include(c => c.Department)
            .FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken);
    }

    public async Task<RiskAssessment> CreateRiskAssessmentAsync(Guid claimId, AgentRiskAssessmentOutput assessment, CancellationToken cancellationToken = default)
    {
        var claim = await _context.ExpenseClaims.FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken);
        if (claim == null)
            throw new InvalidOperationException($"Claim {claimId} not found");

        var riskLevelEnum = Enum.TryParse<RiskLevel>(assessment.RiskLevel, out var rLevel) ? rLevel : RiskLevel.REVIEW_REQUIRED;

        var entity = new RiskAssessment
        {
            ExpenseClaimId = claimId,
            RiskScore = assessment.RiskScore,
            RiskLevel = riskLevelEnum,
            DuplicateDetected = assessment.DuplicateDetected,
            UnusualAmountDetected = assessment.UnusualAmountDetected,
            SuspiciousPatternDetected = assessment.SuspiciousPatternDetected,
            ReasonSummary = assessment.ReasonSummary,
            SignalsJson = JsonSerializer.Serialize(assessment.Signals),
            AgentVersion = assessment.AgentVersion,
            CreatedAt = DateTime.UtcNow
        };

        _context.RiskAssessments.Add(entity);

        // Update claim risk status
        claim.RiskStatus = (RiskStatus)Enum.Parse(typeof(RiskStatus), riskLevelEnum.ToString());
        claim.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<AuditLog> CreateRiskLogAsync(Guid claimId, string reason, string severity, CancellationToken cancellationToken = default)
    {
        var audit = new AuditLog
        {
            EntityType = "ExpenseClaim",
            EntityId = claimId,
            Action = severity.Equals("CRITICAL", StringComparison.OrdinalIgnoreCase) || severity.Equals("HIGH", StringComparison.OrdinalIgnoreCase)
                ? "ANOMALY_DETECTED"
                : "RISK_LOG_RECORDED",
            ActorRole = "FraudAnomalyRiskAgent",
            ActorUserId = null,
            Details = reason,
            CorrelationId = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow
        };

        _context.AuditLogs.Add(audit);
        await _context.SaveChangesAsync(cancellationToken);
        return audit;
    }
}

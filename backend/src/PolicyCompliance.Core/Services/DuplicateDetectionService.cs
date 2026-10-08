using PolicyCompliance.Core.Agents.Contracts;
using PolicyCompliance.Core.Entities;
using PolicyCompliance.Core.Enums;

namespace PolicyCompliance.Core.Services;

public interface IDuplicateDetectionService
{
    List<DuplicateMatchResult> DetectDuplicates(ExpenseClaim targetClaim, IEnumerable<ExpenseClaim> existingClaims);
}

public class DuplicateMatchResult
{
    public Guid MatchingClaimId { get; set; }
    public string MatchingClaimNumber { get; set; } = string.Empty;
    public decimal SimilarityScore { get; set; }
    public DuplicateMatchType MatchType { get; set; }
    public string Evidence { get; set; } = string.Empty;
    public decimal MatchingAmount { get; set; }
    public DateTime MatchingClaimDate { get; set; }
    public string MatchingMerchant { get; set; } = string.Empty;
}

public class DuplicateDetectionService : IDuplicateDetectionService
{
    public List<DuplicateMatchResult> DetectDuplicates(ExpenseClaim targetClaim, IEnumerable<ExpenseClaim> existingClaims)
    {
        var matches = new List<DuplicateMatchResult>();

        // Only compare against other claims (different id) and exclude rejected ones if needed, or include them to track repeated submissions
        var candidates = existingClaims
            .Where(c => c.Id != targetClaim.Id && c.Status != ClaimStatus.REJECTED)
            .ToList();

        foreach (var candidate in candidates)
        {
            bool sameEmployee = candidate.EmployeeId == targetClaim.EmployeeId;
            bool sameAmount = Math.Abs(candidate.TotalAmount - targetClaim.TotalAmount) < 0.01m;
            bool sameMerchant = !string.IsNullOrWhiteSpace(candidate.MerchantName) &&
                                candidate.MerchantName.Equals(targetClaim.MerchantName, StringComparison.OrdinalIgnoreCase);
            bool sameDate = candidate.ClaimDate.Date == targetClaim.ClaimDate.Date;
            bool sameCategory = candidate.Category.Equals(targetClaim.Category, StringComparison.OrdinalIgnoreCase);

            // Receipt URL matching across items
            bool sameReceipt = false;
            var targetReceipts = targetClaim.Items.Where(i => !string.IsNullOrWhiteSpace(i.ReceiptUrl)).Select(i => i.ReceiptUrl!.Trim()).ToHashSet();
            var candidateReceipts = candidate.Items.Where(i => !string.IsNullOrWhiteSpace(i.ReceiptUrl)).Select(i => i.ReceiptUrl!.Trim()).ToHashSet();
            if (targetReceipts.Any() && candidateReceipts.Any())
            {
                sameReceipt = targetReceipts.Overlaps(candidateReceipts);
            }

            // Description word overlap similarity (Jaccard similarity)
            double descSimilarity = CalculateStringSimilarity(targetClaim.Description, candidate.Description);

            // Deterministic exact match rule:
            // 1. Same employee + Same merchant + Same date + Same amount => EXACT DUPLICATE
            if (sameEmployee && sameMerchant && sameDate && sameAmount)
            {
                matches.Add(new DuplicateMatchResult
                {
                    MatchingClaimId = candidate.Id,
                    MatchingClaimNumber = candidate.ClaimNumber,
                    SimilarityScore = 1.00m,
                    MatchType = DuplicateMatchType.EXACT,
                    Evidence = $"Exact match: Same Employee, Merchant '{candidate.MerchantName}', Date ({candidate.ClaimDate:yyyy-MM-dd}), and Amount ({candidate.TotalAmount:N2} {candidate.Currency}).",
                    MatchingAmount = candidate.TotalAmount,
                    MatchingClaimDate = candidate.ClaimDate,
                    MatchingMerchant = candidate.MerchantName
                });
                continue;
            }

            // 2. Same receipt uploaded across different claims => EXACT/CRITICAL DUPLICATE
            if (sameReceipt)
            {
                matches.Add(new DuplicateMatchResult
                {
                    MatchingClaimId = candidate.Id,
                    MatchingClaimNumber = candidate.ClaimNumber,
                    SimilarityScore = 0.98m,
                    MatchType = DuplicateMatchType.EXACT,
                    Evidence = $"Identical receipt document found attached to Claim #{candidate.ClaimNumber}.",
                    MatchingAmount = candidate.TotalAmount,
                    MatchingClaimDate = candidate.ClaimDate,
                    MatchingMerchant = candidate.MerchantName
                });
                continue;
            }

            // 3. Partial / High Similarity Match
            decimal score = 0m;
            var evidencePoints = new List<string>();

            if (sameEmployee) score += 0.20m;
            if (sameAmount)
            {
                score += 0.35m;
                evidencePoints.Add($"identical amount ({candidate.TotalAmount:N2})");
            }
            if (sameMerchant)
            {
                score += 0.25m;
                evidencePoints.Add($"same merchant '{candidate.MerchantName}'");
            }
            if (Math.Abs((candidate.ClaimDate.Date - targetClaim.ClaimDate.Date).TotalDays) <= 2)
            {
                score += 0.15m;
                evidencePoints.Add("dates within 48 hours");
            }
            if (descSimilarity >= 0.70)
            {
                score += 0.15m;
                evidencePoints.Add($"high description similarity ({descSimilarity:P0})");
            }

            if (score >= 0.70m)
            {
                matches.Add(new DuplicateMatchResult
                {
                    MatchingClaimId = candidate.Id,
                    MatchingClaimNumber = candidate.ClaimNumber,
                    SimilarityScore = Math.Min(score, 0.95m),
                    MatchType = DuplicateMatchType.PARTIAL,
                    Evidence = $"Partial duplicate: " + string.Join(", ", evidencePoints) + ".",
                    MatchingAmount = candidate.TotalAmount,
                    MatchingClaimDate = candidate.ClaimDate,
                    MatchingMerchant = candidate.MerchantName
                });
            }
        }

        return matches.OrderByDescending(m => m.SimilarityScore).ToList();
    }

    private static double CalculateStringSimilarity(string? s1, string? s2)
    {
        if (string.IsNullOrWhiteSpace(s1) || string.IsNullOrWhiteSpace(s2))
            return 0.0;

        var set1 = s1.ToLowerInvariant().Split(new[] { ' ', ',', '.', ';', '-' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var set2 = s2.ToLowerInvariant().Split(new[] { ' ', ',', '.', ';', '-' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        if (!set1.Any() || !set2.Any()) return 0.0;

        int intersection = set1.Count(w => set2.Contains(w));
        int union = set1.Union(set2).Count();

        return (double)intersection / union;
    }
}

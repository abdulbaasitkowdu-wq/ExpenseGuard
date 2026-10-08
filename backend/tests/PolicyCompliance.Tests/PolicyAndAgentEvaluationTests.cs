using FluentAssertions;
using Moq;
using PolicyCompliance.Core.Agents.Contracts;
using PolicyCompliance.Core.Agents.FraudAnomalyRiskAgent;
using PolicyCompliance.Core.Entities;
using PolicyCompliance.Core.Enums;
using PolicyCompliance.Core.Services;
using PolicyCompliance.Core.Tools;
using PolicyCompliance.Core.Validators;
using Xunit;

namespace PolicyCompliance.Tests;

public class PolicyValidationTests
{
    private readonly PolicyValidationEngine _engine = new();

    private readonly List<ExpensePolicy> _policies = new()
    {
        new ExpensePolicy
        {
            Id = Guid.NewGuid(),
            PolicyName = "Meals Policy",
            Category = "Meals",
            MaximumAmount = 15000m,
            RequiresReceipt = true,
            RequiresManagerApproval = true,
            AllowedCurrency = "LKR",
            Active = true
        },
        new ExpensePolicy
        {
            Id = Guid.NewGuid(),
            PolicyName = "Hotel Policy",
            Category = "Hotel",
            MaximumAmount = 50000m,
            RequiresReceipt = true,
            RequiresManagerApproval = true,
            AllowedCurrency = "LKR",
            Active = true
        }
    };

    [Fact]
    public void ValidateClaim_WhenWithinLimitsWithReceipt_ReturnsCompliant()
    {
        var claim = new ExpenseClaim
        {
            Id = Guid.NewGuid(),
            Category = "Meals",
            TotalAmount = 8500m,
            Currency = "LKR",
            Items = new List<ExpenseItem>
            {
                new() { Category = "Meals", Amount = 8500m, ReceiptUrl = "https://receipts.internal/1.pdf" }
            }
        };

        var result = _engine.ValidateClaim(claim, _policies, 100000m, 500000m);

        result.IsCompliant.Should().BeTrue();
        result.Violations.Should().BeEmpty();
    }

    [Fact]
    public void ValidateClaim_WhenMealExceedsConfiguredMaximum_ReturnsPolicyViolation()
    {
        var claim = new ExpenseClaim
        {
            Id = Guid.NewGuid(),
            Category = "Meals",
            TotalAmount = 25000m, // Policy limit is 15000
            Currency = "LKR",
            Items = new List<ExpenseItem>
            {
                new() { Category = "Meals", Amount = 25000m, ReceiptUrl = "https://receipts.internal/1.pdf" }
            }
        };

        var result = _engine.ValidateClaim(claim, _policies, 100000m, 500000m);

        result.IsCompliant.Should().BeFalse();
        result.Violations.Should().Contain(v => v.RuleCode == "RULE_MAX_AMOUNT_EXCEEDED");
    }

    [Fact]
    public void ValidateClaim_WhenReceiptIsMissing_ReturnsReceiptViolation()
    {
        var claim = new ExpenseClaim
        {
            Id = Guid.NewGuid(),
            Category = "Meals",
            TotalAmount = 5000m,
            Currency = "LKR",
            Items = new List<ExpenseItem>
            {
                new() { Category = "Meals", Amount = 5000m, ReceiptUrl = null } // Missing receipt
            }
        };

        var result = _engine.ValidateClaim(claim, _policies, 100000m, 500000m);

        result.IsCompliant.Should().BeFalse();
        result.Violations.Should().Contain(v => v.RuleCode == "RULE_MISSING_RECEIPT");
    }

    [Fact]
    public void ValidateClaim_WhenCategoryIsNotAllowed_ReturnsCategoryDisallowedViolation()
    {
        var claim = new ExpenseClaim
        {
            Id = Guid.NewGuid(),
            Category = "LuxuryWatches", // Not in policy
            TotalAmount = 10000m,
            Currency = "LKR",
            Items = new List<ExpenseItem>
            {
                new() { Category = "LuxuryWatches", Amount = 10000m, ReceiptUrl = "https://receipts.internal/watch.pdf" }
            }
        };

        var result = _engine.ValidateClaim(claim, _policies, 100000m, 500000m);

        result.IsCompliant.Should().BeFalse();
        result.Violations.Should().Contain(v => v.RuleCode == "RULE_CATEGORY_DISALLOWED");
    }

    [Fact]
    public void ValidateClaim_WhenCurrencyIsDisallowed_ReturnsCurrencyViolation()
    {
        var claim = new ExpenseClaim
        {
            Id = Guid.NewGuid(),
            Category = "Meals",
            TotalAmount = 50m,
            Currency = "EUR", // Expected LKR
            Items = new List<ExpenseItem>
            {
                new() { Category = "Meals", Amount = 50m, Currency = "EUR", ReceiptUrl = "https://receipts.internal/1.pdf" }
            }
        };

        var result = _engine.ValidateClaim(claim, _policies, 100000m, 500000m);

        result.IsCompliant.Should().BeFalse();
        result.Violations.Should().Contain(v => v.RuleCode == "RULE_CURRENCY_DISALLOWED");
    }

    [Fact]
    public void ValidateClaim_WhenExceedsDepartmentMonthlyBudget_ReturnsBudgetExceededViolation()
    {
        var claim = new ExpenseClaim
        {
            Id = Guid.NewGuid(),
            Category = "Meals",
            TotalAmount = 12000m,
            Currency = "LKR",
            Items = new List<ExpenseItem>
            {
                new() { Category = "Meals", Amount = 12000m, ReceiptUrl = "https://receipts.internal/1.pdf" }
            }
        };

        // Department spend 495,000 + 12,000 = 507,000 > Budget 500,000
        var result = _engine.ValidateClaim(claim, _policies, 495000m, 500000m);

        result.Violations.Should().Contain(v => v.RuleCode == "RULE_DEPARTMENT_BUDGET_EXCEEDED");
    }
}

public class DuplicateDetectionTests
{
    private readonly DuplicateDetectionService _service = new();

    [Fact]
    public void DetectDuplicates_WhenExactSameEmployeeMerchantDateAmount_ReturnsExactMatch()
    {
        var empId = Guid.NewGuid();
        var date = new DateTime(2026, 9, 20);

        var existingClaim = new ExpenseClaim
        {
            Id = Guid.NewGuid(),
            ClaimNumber = "CLM-1001",
            EmployeeId = empId,
            MerchantName = "ABC Hotel",
            ClaimDate = date,
            TotalAmount = 25000m,
            Category = "Hotel",
            Description = "Overnight stay",
            Status = ClaimStatus.APPROVED
        };

        var targetClaim = new ExpenseClaim
        {
            Id = Guid.NewGuid(),
            ClaimNumber = "CLM-1002",
            EmployeeId = empId,
            MerchantName = "ABC Hotel",
            ClaimDate = date,
            TotalAmount = 25000m,
            Category = "Hotel",
            Description = "Overnight stay"
        };

        var matches = _service.DetectDuplicates(targetClaim, new[] { existingClaim });

        matches.Should().NotBeEmpty();
        var match = matches.First();
        match.MatchType.Should().Be(DuplicateMatchType.EXACT);
        match.SimilarityScore.Should().Be(1.00m);
    }

    [Fact]
    public void DetectDuplicates_WhenIdenticalReceiptAttachedToDifferentClaim_ReturnsMatch()
    {
        var existingClaim = new ExpenseClaim
        {
            Id = Guid.NewGuid(),
            ClaimNumber = "CLM-2001",
            EmployeeId = Guid.NewGuid(),
            MerchantName = "Hotel A",
            ClaimDate = DateTime.UtcNow.AddDays(-10),
            TotalAmount = 30000m,
            Category = "Hotel",
            Status = ClaimStatus.APPROVED,
            Items = new List<ExpenseItem>
            {
                new() { ReceiptUrl = "https://cloud.storage/receipts/sep20-hotel-invoice.pdf" }
            }
        };

        var targetClaim = new ExpenseClaim
        {
            Id = Guid.NewGuid(),
            ClaimNumber = "CLM-2002",
            EmployeeId = Guid.NewGuid(),
            MerchantName = "Hotel A",
            ClaimDate = DateTime.UtcNow.AddDays(-2),
            TotalAmount = 30000m,
            Category = "Hotel",
            Items = new List<ExpenseItem>
            {
                new() { ReceiptUrl = "https://cloud.storage/receipts/sep20-hotel-invoice.pdf" }
            }
        };

        var matches = _service.DetectDuplicates(targetClaim, new[] { existingClaim });

        matches.Should().NotBeEmpty();
        matches.First().Evidence.Should().Contain("Identical receipt document");
    }

    [Fact]
    public void DetectDuplicates_WhenCompletelyUnrelatedClaim_ReturnsNoMatches()
    {
        var existingClaim = new ExpenseClaim
        {
            Id = Guid.NewGuid(),
            ClaimNumber = "CLM-3001",
            EmployeeId = Guid.NewGuid(),
            MerchantName = "Office Depot",
            ClaimDate = DateTime.UtcNow.AddDays(-40),
            TotalAmount = 1200m,
            Category = "Supplies",
            Status = ClaimStatus.APPROVED
        };

        var targetClaim = new ExpenseClaim
        {
            Id = Guid.NewGuid(),
            ClaimNumber = "CLM-3002",
            EmployeeId = Guid.NewGuid(),
            MerchantName = "Uber",
            ClaimDate = DateTime.UtcNow,
            TotalAmount = 850m,
            Category = "Travel"
        };

        var matches = _service.DetectDuplicates(targetClaim, new[] { existingClaim });

        matches.Should().BeEmpty();
    }
}

public class AgentEvaluationGoldenCasesTests
{
    [Fact]
    public async Task GoldenCase1_NormalClaim_ReturnsLowRisk()
    {
        var mockTools = new Mock<IComplianceAgentTools>();
        var claimId = Guid.NewGuid();
        var empId = Guid.NewGuid();
        var deptId = Guid.NewGuid();

        var claim = new ExpenseClaim
        {
            Id = claimId,
            EmployeeId = empId,
            DepartmentId = deptId,
            TotalAmount = 5000m,
            Currency = "LKR",
            Category = "Meals",
            MerchantName = "Cafe Verandah"
        };

        mockTools.Setup(t => t.GetClaimDetailsAsync(claimId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(claim);

        mockTools.Setup(t => t.GetEmployeeExpenseHistoryAsync(empId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentClaimHistoryItem>
            {
                new() { Category = "Meals", Amount = 4500m, Merchant = "Cafe Verandah" },
                new() { Category = "Meals", Amount = 5200m, Merchant = "Colombo Diner" }
            });

        mockTools.Setup(t => t.GetDepartmentSpendingStatisticsAsync(deptId, "Meals", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentDepartmentCategoryStats { AverageAmount = 5500m, P90Amount = 10000m, ClaimCount = 10 });

        mockTools.Setup(t => t.FindPossibleDuplicateClaimsAsync(claimId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentDuplicateCandidate>());

        var agent = new FraudAnomalyRiskAgent(mockTools.Object);
        var result = await agent.AnalyzeClaimAsync(claimId);

        result.RiskLevel.Should().Be("LOW_RISK");
        result.DuplicateDetected.Should().BeFalse();
        result.UnusualAmountDetected.Should().BeFalse();
        result.RiskScore.Should().BeLessThan(35);
    }

    [Fact]
    public async Task GoldenCase2_ExactDuplicate_ReturnsReviewRequiredWithDuplicateSignal()
    {
        var mockTools = new Mock<IComplianceAgentTools>();
        var claimId = Guid.NewGuid();
        var empId = Guid.NewGuid();
        var deptId = Guid.NewGuid();

        var claim = new ExpenseClaim
        {
            Id = claimId,
            EmployeeId = empId,
            DepartmentId = deptId,
            TotalAmount = 25000m,
            Currency = "LKR",
            Category = "Hotel",
            MerchantName = "ABC Hotel"
        };

        mockTools.Setup(t => t.GetClaimDetailsAsync(claimId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(claim);

        mockTools.Setup(t => t.FindPossibleDuplicateClaimsAsync(claimId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentDuplicateCandidate>
            {
                new()
                {
                    MatchingClaimId = Guid.NewGuid().ToString(),
                    MatchingClaimNumber = "CLM-2026-1002",
                    Amount = 25000m,
                    ClaimDate = "2026-09-20",
                    Merchant = "ABC Hotel",
                    SimilarityScore = 1.00m,
                    MatchType = "EXACT",
                    Evidence = "Exact duplicate match"
                }
            });

        var agent = new FraudAnomalyRiskAgent(mockTools.Object);
        var result = await agent.AnalyzeClaimAsync(claimId);

        result.RiskLevel.Should().Be("REVIEW_REQUIRED");
        result.DuplicateDetected.Should().BeTrue();
        result.Signals.Should().Contain(s => s.Type == "DUPLICATE" && s.Severity == "CRITICAL");
    }

    [Fact]
    public async Task GoldenCase3_ExtremeAmount_ReturnsHighRiskWithUnusualAmountSignal()
    {
        var mockTools = new Mock<IComplianceAgentTools>();
        var claimId = Guid.NewGuid();
        var empId = Guid.NewGuid();
        var deptId = Guid.NewGuid();

        var claim = new ExpenseClaim
        {
            Id = claimId,
            EmployeeId = empId,
            DepartmentId = deptId,
            TotalAmount = 85000m, // Historical is ~15,000
            Currency = "LKR",
            Category = "Meals",
            MerchantName = "Luxury Banquet"
        };

        mockTools.Setup(t => t.GetClaimDetailsAsync(claimId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(claim);

        mockTools.Setup(t => t.GetEmployeeExpenseHistoryAsync(empId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentClaimHistoryItem>
            {
                new() { Category = "Meals", Amount = 14000m, Merchant = "Restaurant A" },
                new() { Category = "Meals", Amount = 15000m, Merchant = "Restaurant B" },
                new() { Category = "Meals", Amount = 16000m, Merchant = "Restaurant C" }
            });

        mockTools.Setup(t => t.GetDepartmentSpendingStatisticsAsync(deptId, "Meals", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentDepartmentCategoryStats { AverageAmount = 15000m, P90Amount = 25000m, ClaimCount = 20 });

        mockTools.Setup(t => t.FindPossibleDuplicateClaimsAsync(claimId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentDuplicateCandidate>());

        var agent = new FraudAnomalyRiskAgent(mockTools.Object);
        var result = await agent.AnalyzeClaimAsync(claimId);

        result.UnusualAmountDetected.Should().BeTrue();
        result.RiskScore.Should().BeGreaterThanOrEqualTo(60);
        result.Signals.Should().Contain(s => s.Type == "UNUSUAL_AMOUNT");
    }

    [Fact]
    public async Task GoldenCase4_DuplicatePlusUnusualAmount_ReturnsCriticalReviewRequired()
    {
        var mockTools = new Mock<IComplianceAgentTools>();
        var claimId = Guid.NewGuid();
        var empId = Guid.NewGuid();
        var deptId = Guid.NewGuid();

        var claim = new ExpenseClaim
        {
            Id = claimId,
            EmployeeId = empId,
            DepartmentId = deptId,
            TotalAmount = 75000m,
            Currency = "LKR",
            Category = "Hotel",
            MerchantName = "Resort XYZ"
        };

        mockTools.Setup(t => t.GetClaimDetailsAsync(claimId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(claim);

        mockTools.Setup(t => t.GetEmployeeExpenseHistoryAsync(empId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentClaimHistoryItem>
            {
                new() { Category = "Hotel", Amount = 20000m, Merchant = "Hotel A" },
                new() { Category = "Hotel", Amount = 22000m, Merchant = "Hotel B" }
            });

        mockTools.Setup(t => t.FindPossibleDuplicateClaimsAsync(claimId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentDuplicateCandidate>
            {
                new()
                {
                    MatchingClaimId = Guid.NewGuid().ToString(),
                    MatchingClaimNumber = "CLM-2026-9099",
                    Amount = 75000m,
                    SimilarityScore = 1.00m,
                    MatchType = "EXACT",
                    Evidence = "Exact match"
                }
            });

        var agent = new FraudAnomalyRiskAgent(mockTools.Object);
        var result = await agent.AnalyzeClaimAsync(claimId);

        result.DuplicateDetected.Should().BeTrue();
        result.UnusualAmountDetected.Should().BeTrue();
        result.RiskLevel.Should().Be("REVIEW_REQUIRED");
        result.RiskScore.Should().BeGreaterThanOrEqualTo(80);
    }

    [Fact]
    public async Task GoldenCase5_AgentToolFailure_TriggersSafeFallbackAndNeverAutoApproves()
    {
        var mockTools = new Mock<IComplianceAgentTools>();
        var claimId = Guid.NewGuid();

        // Simulate database/tool network failure
        mockTools.Setup(t => t.GetClaimDetailsAsync(claimId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Database connection timed out during tool execution."));

        var agent = new FraudAnomalyRiskAgent(mockTools.Object);
        var result = await agent.AnalyzeClaimAsync(claimId);

        // SAFE FAILURE REQUIREMENT: Never auto-approves or returns LOW_RISK on error!
        result.RiskLevel.Should().Be("REVIEW_REQUIRED");
        result.RiskScore.Should().BeGreaterThanOrEqualTo(80);
        result.ReasonSummary.Should().Contain("SAFE-FAILURE PAUSE");
        result.Signals.Should().Contain(s => s.Type == "SAFE_FALLBACK");
    }
}

public class ApprovalBusinessRulesTests
{
    [Fact]
    public void ApproveClaim_WhenUserIsNotManager_ThrowsUnauthorizedAccessException()
    {
        var employee = new User
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Employee
        };

        // Assert employee cannot execute approval
        Action act = () =>
        {
            if (employee.Role != UserRole.Manager && employee.Role != UserRole.Admin)
                throw new UnauthorizedAccessException("Only authorized managers can approve claims.");
        };

        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void RejectClaim_WhenNoReasonProvided_ThrowsArgumentException()
    {
        Action act = () =>
        {
            string reason = "";
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A specific rejection reason must be provided by the manager.");
        };

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RequestRevision_WhenCommentProvided_TransitionsToRevisionRequired()
    {
        var claim = new ExpenseClaim
        {
            Id = Guid.NewGuid(),
            ClaimNumber = "CLM-2026-8080",
            Status = ClaimStatus.WAITING_FOR_MANAGER_APPROVAL
        };

        string feedback = "Attach hotel folio receipt";
        claim.Status = ClaimStatus.REVISION_REQUIRED;
        claim.LatestRevisionComment = feedback;

        claim.Status.Should().Be(ClaimStatus.REVISION_REQUIRED);
        claim.LatestRevisionComment.Should().Be(feedback);
    }
}

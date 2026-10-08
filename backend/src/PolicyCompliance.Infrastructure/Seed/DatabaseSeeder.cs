using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PolicyCompliance.Core.Entities;
using PolicyCompliance.Core.Enums;
using PolicyCompliance.Infrastructure.Data;

namespace PolicyCompliance.Infrastructure.Seed;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        if (await context.Departments.AnyAsync())
            return; // Already seeded

        // 1. Departments
        var deptSales = new Department
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Sales & Marketing",
            Code = "MKT",
            MonthlyBudgetLimit = 2500000m,
            CreatedAt = DateTime.UtcNow.AddMonths(-6)
        };

        var deptEng = new Department
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = "Engineering",
            Code = "ENG",
            MonthlyBudgetLimit = 1800000m,
            CreatedAt = DateTime.UtcNow.AddMonths(-6)
        };

        var deptOps = new Department
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Name = "Finance & Operations",
            Code = "OPS",
            MonthlyBudgetLimit = 1200000m,
            CreatedAt = DateTime.UtcNow.AddMonths(-6)
        };

        context.Departments.AddRange(deptSales, deptEng, deptOps);
        await context.SaveChangesAsync();

        // 2. Users
        string passwordHash = HashPassword("Password123!");

        var managerSarah = new User
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Username = "sarah.chen",
            FullName = "Sarah Chen",
            Email = "sarah.chen@enterprise.com",
            PasswordHash = passwordHash,
            Role = UserRole.Manager,
            DepartmentId = deptSales.Id,
            CreatedAt = DateTime.UtcNow.AddMonths(-6)
        };

        var managerDavid = new User
        {
            Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            Username = "david.miller",
            FullName = "David Miller",
            Email = "david.miller@enterprise.com",
            PasswordHash = passwordHash,
            Role = UserRole.Manager,
            DepartmentId = deptEng.Id,
            CreatedAt = DateTime.UtcNow.AddMonths(-6)
        };

        var empJohn = new User
        {
            Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
            Username = "john.doe",
            FullName = "John Doe",
            Email = "john.doe@enterprise.com",
            PasswordHash = passwordHash,
            Role = UserRole.Employee,
            DepartmentId = deptSales.Id,
            CreatedAt = DateTime.UtcNow.AddMonths(-6)
        };

        var empJane = new User
        {
            Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
            Username = "jane.smith",
            FullName = "Jane Smith",
            Email = "jane.smith@enterprise.com",
            PasswordHash = passwordHash,
            Role = UserRole.Employee,
            DepartmentId = deptEng.Id,
            CreatedAt = DateTime.UtcNow.AddMonths(-6)
        };

        var financeMichael = new User
        {
            Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
            Username = "michael.scott",
            FullName = "Michael Scott",
            Email = "michael.scott@enterprise.com",
            PasswordHash = passwordHash,
            Role = UserRole.Finance,
            DepartmentId = deptOps.Id,
            CreatedAt = DateTime.UtcNow.AddMonths(-6)
        };

        context.Users.AddRange(managerSarah, managerDavid, empJohn, empJane, financeMichael);
        await context.SaveChangesAsync();

        // 3. Expense Policies
        var policyMeal = new ExpensePolicy
        {
            Id = Guid.NewGuid(),
            PolicyName = "Corporate Meal & Sustenance Policy",
            Category = "Meals",
            MaximumAmount = 15000m,
            RequiresReceipt = true,
            RequiresManagerApproval = true,
            AllowedCurrency = "LKR",
            Active = true,
            IsGlobalPolicy = true
        };

        var policyHotel = new ExpensePolicy
        {
            Id = Guid.NewGuid(),
            PolicyName = "Business Travel & Hotel Accommodation",
            Category = "Hotel",
            MaximumAmount = 50000m,
            RequiresReceipt = true,
            RequiresManagerApproval = true,
            AllowedCurrency = "LKR",
            Active = true,
            IsGlobalPolicy = true
        };

        var policyTravel = new ExpensePolicy
        {
            Id = Guid.NewGuid(),
            PolicyName = "Local Transport & Taxi Reimbursement",
            Category = "Travel",
            MaximumAmount = 20000m,
            RequiresReceipt = true,
            RequiresManagerApproval = false,
            AllowedCurrency = "LKR",
            Active = true,
            IsGlobalPolicy = true
        };

        var policySoftware = new ExpensePolicy
        {
            Id = Guid.NewGuid(),
            PolicyName = "Software Tooling & Subscriptions",
            Category = "Software",
            MaximumAmount = 35000m,
            RequiresReceipt = true,
            RequiresManagerApproval = true,
            AllowedCurrency = "LKR",
            Active = true,
            IsGlobalPolicy = true
        };

        var policyEntertainment = new ExpensePolicy
        {
            Id = Guid.NewGuid(),
            PolicyName = "Client Entertainment & Hospitality",
            Category = "Entertainment",
            DepartmentId = deptSales.Id,
            MaximumAmount = 50000m,
            RequiresReceipt = true,
            RequiresManagerApproval = true,
            AllowedCurrency = "LKR",
            Active = true,
            IsGlobalPolicy = false
        };

        context.ExpensePolicies.AddRange(policyMeal, policyHotel, policyTravel, policySoftware, policyEntertainment);
        await context.SaveChangesAsync();

        // 4. Sample Claims
        // Historical Approved Claim 1 (Normal Claim for John Doe)
        var claim1 = new ExpenseClaim
        {
            Id = Guid.Parse("a1111111-1111-1111-1111-111111111111"),
            EmployeeId = empJohn.Id,
            DepartmentId = deptSales.Id,
            ClaimNumber = "CLM-2026-1001",
            ClaimDate = DateTime.UtcNow.AddDays(-20),
            SubmittedAt = DateTime.UtcNow.AddDays(-20),
            TotalAmount = 8500m,
            Currency = "LKR",
            MerchantName = "Cinnamon Grand Colombo",
            Category = "Meals",
            Description = "Client introductory working lunch",
            Status = ClaimStatus.APPROVED,
            PolicyStatus = PolicyStatus.COMPLIANT,
            RiskStatus = RiskStatus.LOW_RISK,
            CreatedAt = DateTime.UtcNow.AddDays(-20),
            UpdatedAt = DateTime.UtcNow.AddDays(-19)
        };
        claim1.Items.Add(new ExpenseItem
        {
            ExpenseDate = DateTime.UtcNow.AddDays(-20),
            Category = "Meals",
            Merchant = "Cinnamon Grand Colombo",
            Amount = 8500m,
            Currency = "LKR",
            Description = "Buffet meal for 2",
            ReceiptUrl = "https://storage.enterprise.internal/receipts/rec-1001.pdf"
        });

        // Historical Approved Claim 2 (Target for duplicate match)
        var claimOriginalHotel = new ExpenseClaim
        {
            Id = Guid.Parse("b2222222-2222-2222-2222-222222222222"),
            EmployeeId = empJohn.Id,
            DepartmentId = deptSales.Id,
            ClaimNumber = "CLM-2026-1002",
            ClaimDate = new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc),
            SubmittedAt = new DateTime(2026, 9, 20, 11, 0, 0, DateTimeKind.Utc),
            TotalAmount = 25000m,
            Currency = "LKR",
            MerchantName = "ABC Hotel",
            Category = "Hotel",
            Description = "Sales summit overnight lodging",
            Status = ClaimStatus.APPROVED,
            PolicyStatus = PolicyStatus.COMPLIANT,
            RiskStatus = RiskStatus.LOW_RISK,
            CreatedAt = new DateTime(2026, 9, 20, 11, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 9, 21, 14, 0, 0, DateTimeKind.Utc)
        };
        claimOriginalHotel.Items.Add(new ExpenseItem
        {
            ExpenseDate = new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc),
            Category = "Hotel",
            Merchant = "ABC Hotel",
            Amount = 25000m,
            Currency = "LKR",
            Description = "Deluxe room accommodation",
            ReceiptUrl = "https://storage.enterprise.internal/receipts/abc-hotel-sep20.pdf"
        });

        // Active Queue Claim 3: EXACT DUPLICATE (Golden Case 2)
        var claimDuplicate = new ExpenseClaim
        {
            Id = Guid.Parse("c3333333-3333-3333-3333-333333333333"),
            EmployeeId = empJohn.Id,
            DepartmentId = deptSales.Id,
            ClaimNumber = "CLM-2026-1003",
            ClaimDate = new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc),
            SubmittedAt = DateTime.UtcNow.AddHours(-10),
            TotalAmount = 25000m,
            Currency = "LKR",
            MerchantName = "ABC Hotel",
            Category = "Hotel",
            Description = "Sales summit overnight lodging",
            Status = ClaimStatus.WAITING_FOR_MANAGER_APPROVAL,
            PolicyStatus = PolicyStatus.COMPLIANT,
            RiskStatus = RiskStatus.REVIEW_REQUIRED,
            CreatedAt = DateTime.UtcNow.AddHours(-10),
            UpdatedAt = DateTime.UtcNow.AddHours(-10)
        };
        claimDuplicate.Items.Add(new ExpenseItem
        {
            ExpenseDate = new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc),
            Category = "Hotel",
            Merchant = "ABC Hotel",
            Amount = 25000m,
            Currency = "LKR",
            Description = "Deluxe room accommodation",
            ReceiptUrl = "https://storage.enterprise.internal/receipts/abc-hotel-sep20.pdf"
        });
        claimDuplicate.DuplicateMatches.Add(new DuplicateMatch
        {
            MatchingClaimId = claimOriginalHotel.Id,
            SimilarityScore = 1.00m,
            MatchType = DuplicateMatchType.EXACT,
            Evidence = "Exact duplicate detected: Same Employee (John Doe), Merchant (ABC Hotel), Date (2026-09-20), and Amount (25,000.00 LKR).",
            CreatedAt = DateTime.UtcNow.AddHours(-10)
        });
        claimDuplicate.RiskAssessments.Add(new RiskAssessment
        {
            RiskScore = 92,
            RiskLevel = RiskLevel.REVIEW_REQUIRED,
            DuplicateDetected = true,
            UnusualAmountDetected = false,
            SuspiciousPatternDetected = false,
            ReasonSummary = "CRITICAL: Exact duplicate claim identified matching approved Claim #CLM-2026-1002.",
            SignalsJson = JsonSerializer.Serialize(new[]
            {
                new { Type = "DUPLICATE", Severity = "CRITICAL", Evidence = "Exact duplicate detected against Claim #CLM-2026-1002. Matching parameters: Employee, Merchant (ABC Hotel), Date (2026-09-20), and Amount (25,000.00)." }
            }),
            CreatedAt = DateTime.UtcNow.AddHours(-10)
        });

        // Active Queue Claim 4: EXTREME ANOMALY & POLICY VIOLATION (Golden Case 3)
        var claimExtreme = new ExpenseClaim
        {
            Id = Guid.Parse("d4444444-4444-4444-4444-444444444444"),
            EmployeeId = empJane.Id,
            DepartmentId = deptEng.Id,
            ClaimNumber = "CLM-2026-1004",
            ClaimDate = DateTime.UtcNow.AddDays(-2),
            SubmittedAt = DateTime.UtcNow.AddHours(-8),
            TotalAmount = 65000m,
            Currency = "LKR",
            MerchantName = "Ministry of Crab",
            Category = "Meals",
            Description = "Team engineering celebration dinner",
            Status = ClaimStatus.WAITING_FOR_MANAGER_APPROVAL,
            PolicyStatus = PolicyStatus.VIOLATIONS_FOUND,
            RiskStatus = RiskStatus.HIGH_RISK,
            CreatedAt = DateTime.UtcNow.AddHours(-8),
            UpdatedAt = DateTime.UtcNow.AddHours(-8)
        };
        claimExtreme.Items.Add(new ExpenseItem
        {
            ExpenseDate = DateTime.UtcNow.AddDays(-2),
            Category = "Meals",
            Merchant = "Ministry of Crab",
            Amount = 65000m,
            Currency = "LKR",
            Description = "Dinner banquet for engineering team",
            ReceiptUrl = "https://storage.enterprise.internal/receipts/moc-bill-65k.pdf"
        });
        claimExtreme.Violations.Add(new PolicyViolation
        {
            PolicyId = policyMeal.Id,
            RuleCode = "RULE_MAX_AMOUNT_EXCEEDED",
            Severity = ViolationSeverity.HIGH,
            Message = "Claim amount (65,000.00 LKR) exceeds the policy maximum limit of 15,000.00 LKR for 'Meals'.",
            ActualValue = "65,000.00 LKR",
            AllowedValue = "15,000.00 LKR",
            CreatedAt = DateTime.UtcNow.AddHours(-8)
        });
        claimExtreme.RiskAssessments.Add(new RiskAssessment
        {
            RiskScore = 82,
            RiskLevel = RiskLevel.HIGH_RISK,
            DuplicateDetected = false,
            UnusualAmountDetected = true,
            SuspiciousPatternDetected = false,
            ReasonSummary = "Claim amount (65,000.00 LKR) is an extreme statistical outlier (Z-score > +3.1σ) over historical baseline.",
            SignalsJson = JsonSerializer.Serialize(new[]
            {
                new { Type = "UNUSUAL_AMOUNT", Severity = "HIGH", Evidence = "Claim amount (65,000.00 LKR) is 4.3x higher than meal policy ceiling and employee average." }
            }),
            CreatedAt = DateTime.UtcNow.AddHours(-8)
        });

        // Active Queue Claim 5: THRESHOLD EVASION PATTERN (Suspicious Pattern)
        var claimEvasion = new ExpenseClaim
        {
            Id = Guid.Parse("e5555555-5555-5555-5555-555555555555"),
            EmployeeId = empJohn.Id,
            DepartmentId = deptSales.Id,
            ClaimNumber = "CLM-2026-1005",
            ClaimDate = DateTime.UtcNow.AddDays(-1),
            SubmittedAt = DateTime.UtcNow.AddHours(-4),
            TotalAmount = 49500m,
            Currency = "LKR",
            MerchantName = "Shangri-La Dining",
            Category = "Entertainment",
            Description = "VIP Partner dinner banquet",
            Status = ClaimStatus.WAITING_FOR_MANAGER_APPROVAL,
            PolicyStatus = PolicyStatus.COMPLIANT,
            RiskStatus = RiskStatus.MEDIUM_RISK,
            CreatedAt = DateTime.UtcNow.AddHours(-4),
            UpdatedAt = DateTime.UtcNow.AddHours(-4)
        };
        claimEvasion.Items.Add(new ExpenseItem
        {
            ExpenseDate = DateTime.UtcNow.AddDays(-1),
            Category = "Entertainment",
            Merchant = "Shangri-La Dining",
            Amount = 49500m,
            Currency = "LKR",
            Description = "VIP dinner banquet",
            ReceiptUrl = "https://storage.enterprise.internal/receipts/shangrila-vip.pdf"
        });
        claimEvasion.RiskAssessments.Add(new RiskAssessment
        {
            RiskScore = 55,
            RiskLevel = RiskLevel.MEDIUM_RISK,
            DuplicateDetected = false,
            UnusualAmountDetected = false,
            SuspiciousPatternDetected = true,
            ReasonSummary = "Potential approval threshold evasion pattern: Amount is within 1% below senior management escalation threshold.",
            SignalsJson = JsonSerializer.Serialize(new[]
            {
                new { Type = "THRESHOLD_EVASION", Severity = "MEDIUM", Evidence = "Claim amount (49,500.00 LKR) is just 1% below the 50,000.00 LKR threshold." }
            }),
            CreatedAt = DateTime.UtcNow.AddHours(-4)
        });

        // Active Queue Claim 6: MISSING RECEIPT VIOLATION
        var claimMissingReceipt = new ExpenseClaim
        {
            Id = Guid.Parse("f6666666-6666-6666-6666-666666666666"),
            EmployeeId = empJane.Id,
            DepartmentId = deptEng.Id,
            ClaimNumber = "CLM-2026-1006",
            ClaimDate = DateTime.UtcNow.AddDays(-3),
            SubmittedAt = DateTime.UtcNow.AddHours(-12),
            TotalAmount = 22000m,
            Currency = "LKR",
            MerchantName = "JetBrains",
            Category = "Software",
            Description = "All Products Pack IDE Subscription",
            Status = ClaimStatus.WAITING_FOR_MANAGER_APPROVAL,
            PolicyStatus = PolicyStatus.VIOLATIONS_FOUND,
            RiskStatus = RiskStatus.LOW_RISK,
            CreatedAt = DateTime.UtcNow.AddHours(-12),
            UpdatedAt = DateTime.UtcNow.AddHours(-12)
        };
        claimMissingReceipt.Items.Add(new ExpenseItem
        {
            ExpenseDate = DateTime.UtcNow.AddDays(-3),
            Category = "Software",
            Merchant = "JetBrains",
            Amount = 22000m,
            Currency = "LKR",
            Description = "JetBrains IDE Subscription",
            ReceiptUrl = null // Missing receipt!
        });
        claimMissingReceipt.Violations.Add(new PolicyViolation
        {
            PolicyId = policySoftware.Id,
            RuleCode = "RULE_MISSING_RECEIPT",
            Severity = ViolationSeverity.HIGH,
            Message = "Policy requires a valid receipt / supporting proof of spend for this claim.",
            ActualValue = "No receipt uploaded",
            AllowedValue = "Valid receipt URL / document required",
            CreatedAt = DateTime.UtcNow.AddHours(-12)
        });

        // Active Queue Claim 7: REVISION REQUIRED
        var claimRevision = new ExpenseClaim
        {
            Id = Guid.Parse("97777777-7777-7777-7777-777777777777"),
            EmployeeId = empJohn.Id,
            DepartmentId = deptSales.Id,
            ClaimNumber = "CLM-2026-1007",
            ClaimDate = DateTime.UtcNow.AddDays(-5),
            SubmittedAt = DateTime.UtcNow.AddDays(-4),
            TotalAmount = 38000m,
            Currency = "LKR",
            MerchantName = "SriLankan Airlines",
            Category = "Travel",
            Description = "Regional client pitch travel",
            Status = ClaimStatus.REVISION_REQUIRED,
            PolicyStatus = PolicyStatus.COMPLIANT,
            RiskStatus = RiskStatus.LOW_RISK,
            LatestRevisionComment = "Please attach your itemized boarding pass and travel pre-authorization approval document.",
            CreatedAt = DateTime.UtcNow.AddDays(-4),
            UpdatedAt = DateTime.UtcNow.AddDays(-3)
        };
        claimRevision.Items.Add(new ExpenseItem
        {
            ExpenseDate = DateTime.UtcNow.AddDays(-5),
            Category = "Travel",
            Merchant = "SriLankan Airlines",
            Amount = 38000m,
            Currency = "LKR",
            Description = "Return ticket Colombo to Jaffna",
            ReceiptUrl = "https://storage.enterprise.internal/receipts/flight-ticket.pdf"
        });
        claimRevision.Reviews.Add(new ManagerReview
        {
            ManagerId = managerSarah.Id,
            Decision = ReviewDecision.REQUEST_REVISION,
            Comment = "Please attach your itemized boarding pass and travel pre-authorization approval document.",
            ReviewedAt = DateTime.UtcNow.AddDays(-3)
        });

        context.ExpenseClaims.AddRange(
            claim1,
            claimOriginalHotel,
            claimDuplicate,
            claimExtreme,
            claimEvasion,
            claimMissingReceipt,
            claimRevision
        );

        // Add audit logs for all claims to show full timeline
        var demoClaims = new[] { claim1, claimOriginalHotel, claimDuplicate, claimExtreme, claimEvasion, claimMissingReceipt, claimRevision };
        foreach (var c in demoClaims)
        {
            context.AuditLogs.Add(new AuditLog
            {
                EntityType = "ExpenseClaim",
                EntityId = c.Id,
                Action = "CLAIM_SUBMITTED",
                ActorUserId = c.EmployeeId,
                ActorRole = "Employee",
                OldStatus = "",
                NewStatus = "SUBMITTED",
                Details = $"Claim {c.ClaimNumber} created for {c.TotalAmount:N2} {c.Currency}.",
                CorrelationId = Guid.NewGuid().ToString(),
                Timestamp = c.CreatedAt
            });

            context.AuditLogs.Add(new AuditLog
            {
                EntityType = "ExpenseClaim",
                EntityId = c.Id,
                Action = "POLICY_CHECK_STARTED",
                ActorRole = "System",
                OldStatus = "SUBMITTED",
                NewStatus = "POLICY_VALIDATING",
                Details = "Deterministic policy validation rules executed against corporate spending rules.",
                CorrelationId = Guid.NewGuid().ToString(),
                Timestamp = c.CreatedAt.AddSeconds(2)
            });

            if (c.Violations.Any())
            {
                context.AuditLogs.Add(new AuditLog
                {
                    EntityType = "ExpenseClaim",
                    EntityId = c.Id,
                    Action = "POLICY_VIOLATION_FOUND",
                    ActorRole = "System",
                    Details = $"Violations flagged: {string.Join(", ", c.Violations.Select(v => v.RuleCode))}",
                    CorrelationId = Guid.NewGuid().ToString(),
                    Timestamp = c.CreatedAt.AddSeconds(4)
                });
            }

            context.AuditLogs.Add(new AuditLog
            {
                EntityType = "ExpenseClaim",
                EntityId = c.Id,
                Action = "RISK_CHECK_STARTED",
                ActorRole = "FraudAnomalyRiskAgent",
                OldStatus = "POLICY_VALIDATING",
                NewStatus = "RISK_ASSESSING",
                Details = "Fraud/Anomaly-Risk Agent initiated multi-agent tool calls and statistical anomaly analysis.",
                CorrelationId = Guid.NewGuid().ToString(),
                Timestamp = c.CreatedAt.AddSeconds(5)
            });

            if (c.RiskAssessments.Any())
            {
                var ra = c.RiskAssessments.First();
                context.AuditLogs.Add(new AuditLog
                {
                    EntityType = "ExpenseClaim",
                    EntityId = c.Id,
                    Action = "RISK_ASSESSMENT_CREATED",
                    ActorRole = "FraudAnomalyRiskAgent",
                    Details = $"Risk assessment finalized. Score: {ra.RiskScore}/100, Level: {ra.RiskLevel}. {ra.ReasonSummary}",
                    CorrelationId = Guid.NewGuid().ToString(),
                    Timestamp = c.CreatedAt.AddSeconds(8)
                });
            }

            if (c.Status == ClaimStatus.WAITING_FOR_MANAGER_APPROVAL)
            {
                context.AuditLogs.Add(new AuditLog
                {
                    EntityType = "ExpenseClaim",
                    EntityId = c.Id,
                    Action = "APPROVAL_REQUESTED",
                    ActorRole = "WorkflowEngine",
                    OldStatus = "RISK_ASSESSING",
                    NewStatus = "WAITING_FOR_MANAGER_APPROVAL",
                    Details = "Human approval pause active. Workflow safely paused awaiting manager sign-off.",
                    CorrelationId = Guid.NewGuid().ToString(),
                    Timestamp = c.CreatedAt.AddSeconds(10)
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password + "PolicySalt2026!");
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}

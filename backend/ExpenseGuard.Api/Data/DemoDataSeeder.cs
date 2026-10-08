using ExpenseGuard.Api.Models;
using ExpenseGuard.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace ExpenseGuard.Api.Data;

public static class DemoDataSeeder
{
    public const string Password = "Password123!";
    public const string CompanyName = "Northstar Digital Group";
    public const decimal UsdToLkr = 300m;
    public const int FiscalYear = 2026;

    public static decimal ToLkr(decimal usd) => decimal.Round(usd * UsdToLkr, 2);

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Departments.AnyAsync(d => d.Code == "EXEC", ct) &&
            await db.Employees.CountAsync(ct) >= 29)
        {
            await EnsureFinanceIsFinalAsync(db, ct);
            await EnsureDepartmentBudgetsAsync(db, ct);
            await ReevaluateUnknownPurchaseRequestsAsync(db, ct);
            await BackfillPurchaseRequestHistoryAsync(db, ct);
            await BackfillPurchaseRequestWorkflowsAsync(db, ct);
            return;
        }

        await ResetOperationalDataAsync(db, ct);

        var roles = await db.Roles.ToDictionaryAsync(r => r.RoleName, ct);
        var employeeRole = roles[RoleNames.Employee];
        var managerRole = roles[RoleNames.Manager];
        var headRole = roles[RoleNames.DepartmentHead];
        var financeRole = roles[RoleNames.Finance];
        var adminRole = roles[RoleNames.Admin];

        var departments = new Dictionary<string, Department>
        {
            ["EXEC"] = new() { Code = "EXEC", DepartmentName = "Executive" },
            ["ENG"] = new() { Code = "ENG", DepartmentName = "Engineering & IT" },
            ["MKT"] = new() { Code = "MKT", DepartmentName = "Marketing" },
            ["SALES"] = new() { Code = "SALES", DepartmentName = "Sales & Business Development" },
            ["FIN"] = new() { Code = "FIN", DepartmentName = "Finance" },
            ["HR"] = new() { Code = "HR", DepartmentName = "Human Resources" },
            ["OPS"] = new() { Code = "OPS", DepartmentName = "Operations & Administration" },
        };
        db.Departments.AddRange(departments.Values);
        await db.SaveChangesAsync(ct);

        var titles = People.Select(p => p.Title).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var designations = titles.ToDictionary(title => title, title => new Designation { Name = title, IsActive = true }, StringComparer.OrdinalIgnoreCase);
        db.Designations.AddRange(designations.Values);
        await db.SaveChangesAsync(ct);

        var hash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 12);
        var roleFor = new Dictionary<string, Role>(StringComparer.OrdinalIgnoreCase)
        {
            [RoleNames.Employee] = employeeRole,
            [RoleNames.Manager] = managerRole,
            [RoleNames.DepartmentHead] = headRole,
            [RoleNames.Finance] = financeRole,
            [RoleNames.Admin] = adminRole,
        };

        var employees = new Dictionary<string, Employee>(StringComparer.OrdinalIgnoreCase);
        foreach (var person in People)
        {
            var employee = new Employee
            {
                Username = person.Username,
                NormalizedUsername = person.Username,
                FullName = person.FullName,
                Email = person.Email,
                PasswordHash = hash,
                Role = roleFor[person.Role],
                RoleId = roleFor[person.Role].RoleId,
                Department = departments[person.DepartmentCode],
                DepartmentId = departments[person.DepartmentCode].DepartmentId,
                Designation = designations[person.Title],
                DesignationId = designations[person.Title].DesignationId,
                IsActive = true
            };
            employees[person.Code] = employee;
            db.Employees.Add(employee);
        }
        await db.SaveChangesAsync(ct);

        foreach (var person in People.Where(p => p.ManagerCode is not null))
            employees[person.Code].ManagerId = employees[person.ManagerCode!].EmployeeId;
        await db.SaveChangesAsync(ct);

        db.Policies.AddRange(BuildPolicies(departments));
        await db.SaveChangesAsync(ct);

        db.Budgets.AddRange(BuildBudgets(departments));
        await db.SaveChangesAsync(ct);

        db.ApprovalWorkflowTemplates.Add(new ApprovalWorkflowTemplate
        {
            Name = ClaimIntakeCoordinator.HighValueTemplateName,
            Stages =
            [
                new ApprovalStageDefinition { Sequence = 1, RequiredRole = RoleNames.Manager },
                new ApprovalStageDefinition { Sequence = 2, RequiredRole = RoleNames.DepartmentHead, MinimumAmount = ToLkr(5_000) },
                new ApprovalStageDefinition { Sequence = 3, RequiredRole = RoleNames.Finance }
            ]
        });
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureFinanceIsFinalAsync(AppDbContext db, CancellationToken ct)
    {
        var templates = await db.ApprovalWorkflowTemplates.Include(t => t.Stages).Where(t => t.IsActive).ToListAsync(ct);
        foreach (var template in templates)
        {
            var finance = template.Stages
                .Where(s => s.RequiredRole == RoleNames.Finance)
                .OrderBy(s => s.Sequence)
                .LastOrDefault();
            if (finance is null)
            {
                template.Stages.Add(new ApprovalStageDefinition
                {
                    Sequence = template.Stages.Select(s => s.Sequence).DefaultIfEmpty(0).Max() + 1,
                    RequiredRole = RoleNames.Finance
                });
            }
            else if (finance.MinimumAmount is not null)
                finance.MinimumAmount = null;
        }
        await db.SaveChangesAsync(ct);
    }

    private static readonly (string Code, string Purpose, decimal Q1, decimal Q2, decimal Q3, decimal Q4)[] DepartmentBudgetPlans =
    [
        ("EXEC", "Leadership operations and travel", 100_000, 100_000, 100_000, 100_000),
        ("ENG", "Product engineering, cloud and tooling", 180_000, 190_000, 200_000, 190_000),
        ("MKT", "Campaigns, advertising and events", 120_000, 130_000, 140_000, 130_000),
        ("SALES", "Client acquisition and travel", 140_000, 150_000, 160_000, 150_000),
        ("FIN", "Finance operations and audit", 80_000, 80_000, 85_000, 85_000),
        ("HR", "People operations and training", 60_000, 60_000, 65_000, 65_000),
        ("OPS", "Facilities, vendors and administration", 75_000, 80_000, 80_000, 80_000),
    ];

    public static string BudgetForName(string purpose, string quarterLabel, int year) =>
        $"{purpose} — {quarterLabel} {year}";

    public static decimal[] ScaledQuarterLkr(decimal q1Usd, decimal q2Usd, decimal q3Usd, decimal q4Usd)
    {
        var amounts = new[] { ToLkr(q1Usd), ToLkr(q2Usd), ToLkr(q3Usd), ToLkr(q4Usd) };
        var annual = amounts.Sum();
        if (annual <= BudgetService.MaxDepartmentBudget) return amounts;

        var scale = BudgetService.MaxDepartmentBudget / annual;
        var scaled = amounts.Select(amount => decimal.Round(amount * scale, 2)).ToArray();
        scaled[3] = BudgetService.MaxDepartmentBudget - scaled[0] - scaled[1] - scaled[2];
        return scaled;
    }

    private static async Task EnsureDepartmentBudgetsAsync(AppDbContext db, CancellationToken ct)
    {
        var purposes = DepartmentBudgetPlans.ToDictionary(p => p.Code, p => p.Purpose, StringComparer.OrdinalIgnoreCase);
        var budgets = await db.Budgets.Include(b => b.Department).Include(b => b.Transactions).ToListAsync(ct);
        foreach (var group in budgets.GroupBy(b => new { b.DepartmentId, b.Currency }))
        {
            var department = group.First().Department;
            if (purposes.TryGetValue(department.Code, out var purpose))
            {
                foreach (var budget in group)
                {
                    var quarter = QuarterLabel(budget.PeriodStart);
                    if (quarter is null) continue;
                    budget.Name = BudgetForName(purpose, quarter, budget.PeriodStart.Year);
                }
            }

            var active = group.Where(b => b.IsActive).ToList();
            var total = active.Sum(b => b.AllocatedAmount);
            if (total <= BudgetService.MaxDepartmentBudget) continue;

            var scale = BudgetService.MaxDepartmentBudget / total;
            foreach (var budget in active)
            {
                var floor = budget.ReservedAmount + budget.SpentAmount;
                budget.AllocatedAmount = Math.Max(floor, decimal.Round(budget.AllocatedAmount * scale, 2));
                budget.UpdatedAt = DateTime.UtcNow;
                budget.Version = Guid.NewGuid();
                var allocation = budget.Transactions.FirstOrDefault(t => t.Type == BudgetTransactionType.Allocation);
                if (allocation is not null)
                {
                    allocation.Amount = budget.AllocatedAmount;
                    allocation.AllocatedBalance = budget.AllocatedAmount;
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task ReevaluateUnknownPurchaseRequestsAsync(AppDbContext db, CancellationToken ct)
    {
        var unmatched = await db.PurchaseRequests.AsNoTracking()
            .Where(p => p.Status == PurchaseRequestStatus.Submitted
                && (p.Category == null || p.Category == "" || p.Category == "Unknown"))
            .Select(p => new { p.PurchaseRequestId, p.EmployeeId })
            .ToListAsync(ct);
        if (unmatched.Count == 0) return;

        var intake = new PurchaseRequestIntakeCoordinator(db);
        foreach (var item in unmatched)
            await intake.AfterSubmitAsync(item.PurchaseRequestId, item.EmployeeId, ct);
    }

    private static async Task BackfillPurchaseRequestHistoryAsync(AppDbContext db, CancellationToken ct)
    {
        var missing = await db.PurchaseRequests.Where(p => !p.StatusHistory.Any()).ToListAsync(ct);
        foreach (var item in missing)
        {
            item.StatusHistory.Add(new PurchaseRequestStatusHistory
            {
                FromStatus = PurchaseRequestStatus.Draft,
                ToStatus = item.Status,
                ChangedByEmployeeId = item.EmployeeId,
                Reason = item.Status == PurchaseRequestStatus.Draft ? "Created" : item.Status.ToString(),
                ChangedAt = item.SubmittedAt ?? item.CreatedAt
            });
        }
        if (missing.Count > 0) await db.SaveChangesAsync(ct);
    }

    private static async Task BackfillPurchaseRequestWorkflowsAsync(AppDbContext db, CancellationToken ct)
    {
        var existing = await db.WorkflowExecutions.AsNoTracking()
            .Where(w => w.PurchaseRequestId != null)
            .Select(w => w.PurchaseRequestId!.Value)
            .ToListAsync(ct);
        var missing = await db.PurchaseRequests.AsNoTracking()
            .Where(p => p.ReviewJson != null && !existing.Contains(p.PurchaseRequestId))
            .Select(p => new { p.PurchaseRequestId, p.EmployeeId })
            .ToListAsync(ct);
        if (missing.Count == 0) return;

        var intake = new PurchaseRequestIntakeCoordinator(db, null, new WorkflowLedger(db));
        foreach (var item in missing)
            await intake.EnsureWorkflowAsync(item.PurchaseRequestId, item.EmployeeId, ct);
    }

    private static string? QuarterLabel(DateOnly start) => start.Month switch
    {
        1 => "Q1",
        4 => "Q2",
        7 => "Q3",
        10 => "Q4",
        _ => null
    };

    private static IEnumerable<Policy> BuildPolicies(IReadOnlyDictionary<string, Department> departments)
    {
        var from = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Policy Make(string code, string category, decimal max, int? departmentId, int priority) => new()
        {
            PolicyCode = code,
            Version = 1,
            Category = category,
            MinAmount = 0,
            MaxAmount = max,
            Currency = "LKR",
            ReceiptRequired = true,
            IsActive = true,
            Priority = priority,
            EffectiveFrom = from,
            DepartmentId = departmentId
        };

        foreach (var category in new[]
        {
            "Gambling", "Cryptocurrency", "Political advertising", "Political donations", "Political contributions",
            "Personal software subscriptions", "Personal subscriptions", "Personal advertising", "Personal travel",
            "Personal investments", "Personal expenses", "Personal purchases", "Personal gaming equipment",
            "Personal entertainment", "Personal financial services", "Entertainment subscriptions",
            "Unapproved influencer payments", "Unapproved gifts", "Unapproved employee benefits",
        })
            yield return Make($"NS-DENY-{Slug(category)}", category, 0, null, 100);

        var eng = departments["ENG"].DepartmentId;
        foreach (var category in new[]
        {
            "GitHub", "GitLab", "AWS", "Google Cloud", "Microsoft Azure", "DigitalOcean", "JetBrains", "Figma",
            "Slack", "Microsoft 365", "Atlassian", "Linear", "Docker", "Cloudinary"
        })
            yield return Make($"NS-ENG-{Slug(category)}", category, ToLkr(1_000), eng, 20);
        foreach (var category in new[]
        {
            "SaaS subscription", "Cloud infrastructure", "Hardware", "Conference tickets",
            "Professional training", "AI subscription", "External consultants"
        })
            yield return Make($"NS-ENG-APPR-{Slug(category)}", category, ToLkr(10_000), eng, 15);

        var mkt = departments["MKT"].DepartmentId;
        foreach (var category in new[]
        {
            "Google Ads", "Meta Ads", "LinkedIn Ads", "TikTok Ads", "Canva", "Adobe Creative Cloud", "HubSpot",
            "Mailchimp", "SEMrush", "Ahrefs", "Hootsuite", "Figma", "Shutterstock"
        })
            yield return Make($"NS-MKT-{Slug(category)}", category, ToLkr(5_000), mkt, 20);
        foreach (var category in new[]
        {
            "Advertising campaign", "Influencer campaign", "Events", "Sponsorships", "PR agencies",
            "Marketing consultants", "Premium software subscription"
        })
            yield return Make($"NS-MKT-APPR-{Slug(category)}", category, ToLkr(10_000), mkt, 15);

        var sales = departments["SALES"].DepartmentId;
        foreach (var category in new[]
        {
            "Salesforce", "HubSpot", "LinkedIn", "Zoom", "Microsoft 365", "Slack", "Business travel",
            "Client meals", "Taxi/rideshare", "Hotels"
        })
            yield return Make($"NS-SALES-{Slug(category)}", category, ToLkr(1_500), sales, 20);
        foreach (var category in new[]
        {
            "Client entertainment", "Hotel stays", "Client gifts", "Conferences", "Sponsorships"
        })
            yield return Make($"NS-SALES-APPR-{Slug(category)}", category, ToLkr(10_000), sales, 15);

        var fin = departments["FIN"].DepartmentId;
        foreach (var category in new[]
        {
            "Microsoft 365", "Excel", "QuickBooks", "Xero", "Stripe", "Banking services", "Accounting software",
            "Tax services", "Financial software", "Professional training"
        })
            yield return Make($"NS-FIN-{Slug(category)}", category, ToLkr(2_500), fin, 20);
        foreach (var category in new[]
        {
            "Financial consulting", "External auditors", "New accounting software", "Banking fees", "Professional memberships"
        })
            yield return Make($"NS-FIN-APPR-{Slug(category)}", category, ToLkr(10_000), fin, 15);

        var hr = departments["HR"].DepartmentId;
        foreach (var category in new[]
        {
            "HR software", "LinkedIn", "Job boards", "Recruitment platforms", "Microsoft 365", "Zoom",
            "Employee training", "Recruitment events", "Background-check services"
        })
            yield return Make($"NS-HR-{Slug(category)}", category, ToLkr(1_000), hr, 20);
        foreach (var category in new[]
        {
            "Recruitment agencies", "Employee events", "Conferences", "Employee gifts", "Team-building activities"
        })
            yield return Make($"NS-HR-APPR-{Slug(category)}", category, ToLkr(10_000), hr, 15);

        var ops = departments["OPS"].DepartmentId;
        foreach (var category in new[]
        {
            "Office supplies", "Office equipment", "Cleaning services", "Maintenance", "Microsoft 365", "Zoom",
            "Slack", "Business travel", "Courier services", "Office furniture"
        })
            yield return Make($"NS-OPS-{Slug(category)}", category, ToLkr(1_000), ops, 20);
        foreach (var category in new[]
        {
            "Contractors", "Building maintenance", "Office events", "New vendors"
        })
            yield return Make($"NS-OPS-APPR-{Slug(category)}", category, ToLkr(10_000), ops, 15);

        var exec = departments["EXEC"].DepartmentId;
        foreach (var category in new[]
        {
            "Business travel", "Hotels", "Client entertainment", "Conferences", "Professional memberships",
            "Consulting", "Business software", "Business equipment", "Marketing", "Events", "Corporate services"
        })
            yield return Make($"NS-EXEC-{Slug(category)}", category, ToLkr(10_000), exec, 20);
        foreach (var category in new[] { "New vendors", "Major contracts", "Capital expenditure" })
            yield return Make($"NS-EXEC-APPR-{Slug(category)}", category, ToLkr(50_000), exec, 15);
    }

    private static IEnumerable<Budget> BuildBudgets(IReadOnlyDictionary<string, Department> departments)
    {
        (string Label, DateOnly Start, DateOnly End)[] quarters =
        [
            ("Q1", new DateOnly(FiscalYear, 1, 1), new DateOnly(FiscalYear, 3, 31)),
            ("Q2", new DateOnly(FiscalYear, 4, 1), new DateOnly(FiscalYear, 6, 30)),
            ("Q3", new DateOnly(FiscalYear, 7, 1), new DateOnly(FiscalYear, 9, 30)),
            ("Q4", new DateOnly(FiscalYear, 10, 1), new DateOnly(FiscalYear, 12, 31)),
        ];

        foreach (var dept in DepartmentBudgetPlans)
        {
            var amounts = ScaledQuarterLkr(dept.Q1, dept.Q2, dept.Q3, dept.Q4);
            for (var i = 0; i < quarters.Length; i++)
            {
                var amount = amounts[i];
                var quarter = quarters[i];
                var budget = new Budget
                {
                    DepartmentId = departments[dept.Code].DepartmentId,
                    Name = BudgetForName(dept.Purpose, quarter.Label, FiscalYear),
                    PeriodStart = quarter.Start,
                    PeriodEnd = quarter.End,
                    Currency = "LKR",
                    AllocatedAmount = amount
                };
                budget.Transactions.Add(new BudgetTransaction
                {
                    Type = BudgetTransactionType.Allocation,
                    Amount = amount,
                    AllocatedBalance = amount,
                    Description = $"{CompanyName} {quarter.Label} allocation",
                    IdempotencyKey = $"ns:{dept.Code}:{FiscalYear}:{quarter.Label}"
                });
                yield return budget;
            }
        }
    }

    private static async Task ResetOperationalDataAsync(AppDbContext db, CancellationToken ct)
    {
        if (string.Equals(db.Database.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
        {
            await db.Database.ExecuteSqlRawAsync("""
                TRUNCATE TABLE
                  "ToolExecutions",
                  "ValidationResults",
                  "WorkflowSteps",
                  "WorkflowExecutions",
                  "AuditLogs",
                  "PaymentTransactions",
                  "ApprovalSteps",
                  "ApprovalProcesses",
                  "ApprovalStageDefinitions",
                  "ApprovalWorkflowTemplates",
                  "BudgetAlerts",
                  "BudgetTransactions",
                  "Budgets",
                  "PolicyViolations",
                  "PolicyEvaluations",
                  "PolicyDesignations",
                  "Policies",
                  "Receipts",
                  "ClaimStatusHistories",
                  "Reimbursements",
                  "FraudFlags",
                  "FraudEvaluations",
                  "ExpenseClaims",
                  "PurchaseRequests",
                  "Employees",
                  "Designations",
                  "Departments"
                RESTART IDENTITY CASCADE
                """, ct);
            return;
        }

        foreach (var employee in db.Employees)
            employee.ManagerId = null;
        await db.SaveChangesAsync(ct);
        db.RemoveRange(db.ToolExecutions);
        db.RemoveRange(db.ValidationResults);
        db.RemoveRange(db.WorkflowSteps);
        db.RemoveRange(db.WorkflowExecutions);
        db.RemoveRange(db.AuditLogs);
        db.RemoveRange(db.PaymentTransactions);
        db.RemoveRange(db.ApprovalSteps);
        db.RemoveRange(db.ApprovalProcesses);
        db.RemoveRange(db.ApprovalStageDefinitions);
        db.RemoveRange(db.ApprovalWorkflowTemplates);
        db.RemoveRange(db.BudgetAlerts);
        db.RemoveRange(db.BudgetTransactions);
        db.RemoveRange(db.Budgets);
        db.RemoveRange(db.PolicyViolations);
        db.RemoveRange(db.PolicyEvaluations);
        db.RemoveRange(db.PolicyDesignations);
        db.RemoveRange(db.Policies);
        db.RemoveRange(db.Receipts);
        db.RemoveRange(db.ClaimStatusHistories);
        db.RemoveRange(db.Reimbursements);
        db.RemoveRange(db.FraudFlags);
        db.RemoveRange(db.FraudEvaluations);
        db.RemoveRange(db.ExpenseClaims);
        db.RemoveRange(db.PurchaseRequests);
        await db.SaveChangesAsync(ct);
        db.RemoveRange(db.Employees);
        db.RemoveRange(db.Designations);
        db.RemoveRange(db.Departments);
        await db.SaveChangesAsync(ct);
    }

    private static string Slug(string value)
    {
        var chars = value.ToUpperInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        var slug = new string(chars).Replace("--", "-", StringComparison.Ordinal);
        return slug.Length <= 28 ? slug : slug[..28].TrimEnd('-');
    }

    private static readonly Person[] People =
    [
        new("NS-001", "daniel.perera", "Daniel Perera", "daniel.perera@northstar.lk", "Chief Executive Officer", "EXEC", RoleNames.Admin, null),
        new("NS-002", "sarah.fernando", "Sarah Fernando", "sarah.fernando@northstar.lk", "Chief Financial Officer", "EXEC", RoleNames.Admin, "NS-001"),
        new("NS-003", "michael.jayasinghe", "Michael Jayasinghe", "michael.jayasinghe@northstar.lk", "Chief Operating Officer", "EXEC", RoleNames.Admin, "NS-001"),
        new("NS-004", "alex.wijesinghe", "Alex Wijesinghe", "alex.wijesinghe@northstar.lk", "Director of Engineering", "ENG", RoleNames.DepartmentHead, "NS-001"),
        new("NS-005", "kavindu.silva", "Kavindu Silva", "kavindu.silva@northstar.lk", "Engineering Manager", "ENG", RoleNames.Manager, "NS-004"),
        new("NS-006", "nimal.perera", "Nimal Perera", "nimal.perera@northstar.lk", "Senior Software Engineer", "ENG", RoleNames.Employee, "NS-005"),
        new("NS-007", "ruwan.dias", "Ruwan Dias", "ruwan.dias@northstar.lk", "Software Engineer", "ENG", RoleNames.Employee, "NS-005"),
        new("NS-008", "amaya.fernando", "Amaya Fernando", "amaya.fernando@northstar.lk", "Software Engineer", "ENG", RoleNames.Employee, "NS-005"),
        new("NS-009", "tharindu.senanayake", "Tharindu Senanayake", "tharindu.senanayake@northstar.lk", "IT Support Specialist", "ENG", RoleNames.Employee, "NS-005"),
        new("NS-010", "liam.roberts", "Liam Roberts", "liam.roberts@northstar.lk", "Software Engineering Intern", "ENG", RoleNames.Employee, "NS-005"),
        new("NS-011", "emma.williams", "Emma Williams", "emma.williams@northstar.lk", "Marketing Director", "MKT", RoleNames.DepartmentHead, "NS-001"),
        new("NS-012", "hassan.rahman", "Hassan Rahman", "hassan.rahman@northstar.lk", "Marketing Manager", "MKT", RoleNames.Manager, "NS-011"),
        new("NS-013", "ayesha.karim", "Ayesha Karim", "ayesha.karim@northstar.lk", "Digital Marketing Specialist", "MKT", RoleNames.Employee, "NS-012"),
        new("NS-014", "olivia.perera", "Olivia Perera", "olivia.perera@northstar.lk", "Content & Social Media Specialist", "MKT", RoleNames.Employee, "NS-012"),
        new("NS-015", "dilan.fernando", "Dilan Fernando", "dilan.fernando@northstar.lk", "Marketing Intern", "MKT", RoleNames.Employee, "NS-012"),
        new("NS-016", "james.carter", "James Carter", "james.carter@northstar.lk", "Sales Director", "SALES", RoleNames.DepartmentHead, "NS-001"),
        new("NS-017", "farhan.ali", "Farhan Ali", "farhan.ali@northstar.lk", "Sales Manager", "SALES", RoleNames.Manager, "NS-016"),
        new("NS-018", "kevin.silva", "Kevin Silva", "kevin.silva@northstar.lk", "Senior Account Executive", "SALES", RoleNames.Employee, "NS-017"),
        new("NS-019", "natasha.fernando", "Natasha Fernando", "natasha.fernando@northstar.lk", "Account Executive", "SALES", RoleNames.Employee, "NS-017"),
        new("NS-020", "ryan.perera", "Ryan Perera", "ryan.perera@northstar.lk", "Business Development Intern", "SALES", RoleNames.Employee, "NS-017"),
        new("NS-022", "priyantha.silva", "Priyantha Silva", "priyantha.silva@northstar.lk", "Finance Manager", "FIN", RoleNames.Finance, "NS-002"),
        new("NS-023", "maria.perera", "Maria Perera", "maria.perera@northstar.lk", "Financial Analyst", "FIN", RoleNames.Finance, "NS-022"),
        new("NS-024", "sameera.dias", "Sameera Dias", "sameera.dias@northstar.lk", "Accounts Executive", "FIN", RoleNames.Finance, "NS-022"),
        new("NS-025", "jennifer.dias", "Jennifer Dias", "jennifer.dias@northstar.lk", "HR Director", "HR", RoleNames.DepartmentHead, "NS-001"),
        new("NS-026", "fathima.nazeer", "Fathima Nazeer", "fathima.nazeer@northstar.lk", "HR Manager", "HR", RoleNames.Manager, "NS-025"),
        new("NS-027", "ishara.silva", "Ishara Silva", "ishara.silva@northstar.lk", "HR Executive", "HR", RoleNames.Employee, "NS-026"),
        new("NS-028", "robert.perera", "Robert Perera", "robert.perera@northstar.lk", "Operations Director", "OPS", RoleNames.DepartmentHead, "NS-003"),
        new("NS-029", "chamara.fernando", "Chamara Fernando", "chamara.fernando@northstar.lk", "Operations Manager", "OPS", RoleNames.Manager, "NS-028"),
        new("NS-030", "yasmin.khan", "Yasmin Khan", "yasmin.khan@northstar.lk", "Administrative Executive", "OPS", RoleNames.Employee, "NS-029"),
    ];

    private sealed record Person(
        string Code, string Username, string FullName, string Email, string Title,
        string DepartmentCode, string Role, string? ManagerCode);
}

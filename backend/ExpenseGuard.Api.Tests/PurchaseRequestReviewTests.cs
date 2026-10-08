using ExpenseGuard.Api.Contracts;
using ExpenseGuard.Api.Data;
using ExpenseGuard.Api.Models;
using ExpenseGuard.Api.Reimbursements;
using ExpenseGuard.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExpenseGuard.Api.Tests;

public sealed class PurchaseRequestReviewTests
{
    [Fact]
    public async Task Submit_reviews_department_policy_budget_and_fraud_then_queues_manager()
    {
        await using var db = Database();
        var people = await Seed(db);
        await db.Policies.AddRangeAsync(
            new Policy
            {
                PolicyCode = "MKT-ADS", Category = "Advertising", Currency = "USD", MaxAmount = 5000,
                IsActive = true, EffectiveFrom = DateTime.UtcNow.AddYears(-1), DepartmentId = people.DepartmentId
            },
            new Policy
            {
                PolicyCode = "DENY-GAMBLE", Category = "Gambling", Currency = "USD", MaxAmount = 0,
                IsActive = true, EffectiveFrom = DateTime.UtcNow.AddYears(-1)
            });
        db.Budgets.Add(new Budget
        {
            DepartmentId = people.DepartmentId, Name = "Marketing Q", Currency = "USD",
            PeriodStart = new DateOnly(DateTime.UtcNow.Year, 1, 1),
            PeriodEnd = new DateOnly(DateTime.UtcNow.Year, 12, 31),
            AllocatedAmount = 20000
        });
        await db.SaveChangesAsync();

        var service = new PurchaseRequestService(db, new PurchaseRequestIntakeCoordinator(db));
        var created = await service.CreateAsync(new PurchaseRequestWriteDto
        {
            Description = "Advertising campaign with AdCo", EstimatedAmount = 8000, Currency = "USD",
            Vendor = "AdCo", Version = 0
        }, people.EmployeeId, default);
        await service.SubmitAsync(created.PurchaseRequestId, people.EmployeeId, default);

        var submitted = await service.GetAsync(created.PurchaseRequestId, people.EmployeeId, default);
        Assert.Equal(PurchaseRequestStatus.Submitted, submitted.Status);
        Assert.Equal("Advertising", submitted.Category);
        Assert.Equal(RoleNames.Manager, submitted.CurrentRequiredRole);
        Assert.True(submitted.Review!.HasFlags);
        Assert.Contains(submitted.Review.Policy.Flags, f => f.Code == "POLICY_CAP_EXCEEDED");
        Assert.Equal("ok", submitted.Review.Budget.Outcome);
        Assert.Single(await service.ApprovalQueueAsync(RoleNames.Manager, default));

        var decided = await service.DecideAsync(created.PurchaseRequestId, people.ManagerId, RoleNames.Manager,
            new ApprovalDecision("APPROVED", "ok"), default);
        Assert.Equal(PurchaseRequestStatus.Submitted, decided.Status);
        Assert.Equal(RoleNames.Finance, decided.CurrentRequiredRole);
        Assert.Contains(decided.ApprovalSteps, step => step.RequiredRole == RoleNames.Finance);
    }

    [Fact]
    public void Low_value_requests_still_end_with_finance()
    {
        var stages = ApprovalStageSelector.ForAmount(
        [
            new ApprovalStageDefinition { Sequence = 1, RequiredRole = RoleNames.Manager },
            new ApprovalStageDefinition { Sequence = 2, RequiredRole = RoleNames.DepartmentHead, MinimumAmount = 10000 },
            new ApprovalStageDefinition { Sequence = 3, RequiredRole = RoleNames.Finance, MinimumAmount = 10000 }
        ], 100);
        Assert.Equal([RoleNames.Manager, RoleNames.Finance], stages.Select(s => s.RequiredRole).ToArray());
    }

    [Fact]
    public async Task Netflix_request_is_flagged_as_entertainment_policy_breach()
    {
        await using var db = Database();
        var people = await Seed(db);
        db.Policies.Add(new Policy
        {
            PolicyCode = "DENY-ENT", Category = "Entertainment subscriptions", Currency = "LKR", MaxAmount = 0,
            IsActive = true, EffectiveFrom = DateTime.UtcNow.AddYears(-1)
        });
        await db.SaveChangesAsync();
        var service = new PurchaseRequestService(db, new PurchaseRequestIntakeCoordinator(db));
        var created = await service.CreateAsync(new PurchaseRequestWriteDto
        {
            Description = "Please get something from Netflix", EstimatedAmount = 50000, Currency = "LKR",
            Vendor = "Netflix", Version = 0
        }, people.EmployeeId, default);
        await service.SubmitAsync(created.PurchaseRequestId, people.EmployeeId, default);

        var review = (await service.GetAsync(created.PurchaseRequestId, people.EmployeeId, default)).Review!;
        Assert.Equal("Entertainment subscriptions", review.Category);
        Assert.Equal("non_compliant", review.Policy.Outcome);
        Assert.Contains(review.Policy.Flags, f => f.Code == "DENIED_CATEGORY");
        Assert.Contains(review.Fraud.Flags, f => f.Code == "DENIED_CATEGORY_RISK");
    }

    [Fact]
    public async Task Submit_records_a_purchase_request_workflow_execution()
    {
        await using var db = Database();
        var people = await Seed(db);
        var ledger = new WorkflowLedger(db);
        var service = new PurchaseRequestService(db, new PurchaseRequestIntakeCoordinator(db, null, ledger), ledger);
        var created = await service.CreateAsync(new PurchaseRequestWriteDto
        {
            Description = "Office chairs", EstimatedAmount = 1200, Currency = "LKR",
            Vendor = "OfficeMart", Version = 0
        }, people.EmployeeId, default);
        await service.SubmitAsync(created.PurchaseRequestId, people.EmployeeId, default);

        var workflow = await db.WorkflowExecutions.Include(w => w.Steps).SingleAsync();
        Assert.Equal(WorkflowSubjects.PurchaseRequest, workflow.SubjectType);
        Assert.Equal(created.PurchaseRequestId, workflow.PurchaseRequestId);
        Assert.Null(workflow.ExpenseClaimId);
        Assert.Equal("WAITING_FOR_APPROVAL", workflow.Status);
        Assert.Equal(4, workflow.Steps.Count);
        Assert.Contains(workflow.Steps, step => step.Name == "PolicyCompliance");
        Assert.Contains(workflow.Steps, step => step.Name == "HumanApproval");
    }

    [Fact]
    public async Task Forbidden_category_is_flagged_but_still_sent_to_human()
    {
        await using var db = Database();
        var people = await Seed(db);
        db.Policies.Add(new Policy
        {
            PolicyCode = "DENY-GAMBLE", Category = "Gambling", Currency = "USD", MaxAmount = 0,
            IsActive = true, EffectiveFrom = DateTime.UtcNow.AddYears(-1)
        });
        await db.SaveChangesAsync();
        var service = new PurchaseRequestService(db, new PurchaseRequestIntakeCoordinator(db));
        var created = await service.CreateAsync(new PurchaseRequestWriteDto
        {
            Description = "Gambling site sponsorship", EstimatedAmount = 200, Currency = "USD",
            Vendor = "BetCo", Version = 0
        }, people.EmployeeId, default);
        await service.SubmitAsync(created.PurchaseRequestId, people.EmployeeId, default);

        var review = (await service.GetAsync(created.PurchaseRequestId, people.EmployeeId, default)).Review!;
        Assert.Equal("Gambling", review.Category);
        Assert.Contains(review.Policy.Flags, f => f.Code == "DENIED_CATEGORY");
        Assert.Contains(review.Fraud.Flags, f => f.Code == "DENIED_CATEGORY_RISK");
        Assert.Equal(PurchaseRequestStatus.Submitted,
            (await service.GetAsync(created.PurchaseRequestId, people.EmployeeId, default)).Status);
    }

    [Fact]
    public async Task Request_history_stays_private_and_finance_can_read_the_store()
    {
        await using var db = Database();
        var people = await Seed(db);
        var otherRole = await db.Roles.SingleAsync(r => r.RoleName == RoleNames.Employee);
        var financeRole = await db.Roles.SingleAsync(r => r.RoleName == RoleNames.Finance);
        var other = new Employee
        {
            Username = "lee", NormalizedUsername = "LEE", FullName = "Lee", Email = "lee@test.local",
            PasswordHash = "x", DepartmentId = people.DepartmentId, Role = otherRole
        };
        var finance = new Employee
        {
            Username = "fin", NormalizedUsername = "FIN", FullName = "Fin", Email = "fin@test.local",
            PasswordHash = "x", DepartmentId = people.DepartmentId, Role = financeRole
        };
        db.AddRange(other, finance);
        await db.SaveChangesAsync();

        var service = new PurchaseRequestService(db);
        var mine = await service.CreateAsync(new PurchaseRequestWriteDto
        {
            Description = "Office chairs", EstimatedAmount = 100, Currency = "LKR", Vendor = "Ikea", Version = 0
        }, people.EmployeeId, default);
        await service.CreateAsync(new PurchaseRequestWriteDto
        {
            Description = "Other laptop", EstimatedAmount = 200, Currency = "LKR", Vendor = "Dell", Version = 0
        }, other.EmployeeId, default);

        Assert.Single(await service.ListAsync(people.EmployeeId, default));
        Assert.Single(await service.ListAsync(other.EmployeeId, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.HistoryAsync(mine.PurchaseRequestId, other.EmployeeId, RoleNames.Employee, default));

        var ownerHistory = await service.HistoryAsync(mine.PurchaseRequestId, people.EmployeeId, RoleNames.Employee, default);
        Assert.Contains(ownerHistory, h => h.Reason == "Created");
        var financeHistory = await new RequestHistoryService(db).ListAsync(new RequestHistoryQuery(), default);
        Assert.Equal(2, financeHistory.Count);
        Assert.All(financeHistory, item => Assert.Equal("purchase_request", item.Kind));
        _ = finance.EmployeeId;
    }

    private static AppDbContext Database()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static async Task<(int DepartmentId, int EmployeeId, int ManagerId)> Seed(AppDbContext db)
    {
        var department = new Department { Code = "MKT", DepartmentName = "Marketing" };
        var employeeRole = await db.Roles.SingleAsync(r => r.RoleName == RoleNames.Employee);
        var managerRole = await db.Roles.SingleAsync(r => r.RoleName == RoleNames.Manager);
        var employee = new Employee
        {
            Username = "sam", NormalizedUsername = "SAM", FullName = "Sam", Email = "sam@test.local",
            PasswordHash = "x", Department = department, Role = employeeRole
        };
        var manager = new Employee
        {
            Username = "mia", NormalizedUsername = "MIA", FullName = "Mia", Email = "mia@test.local",
            PasswordHash = "x", Department = department, Role = managerRole
        };
        db.AddRange(employee, manager);
        await db.SaveChangesAsync();
        return (department.DepartmentId, employee.EmployeeId, manager.EmployeeId);
    }
}

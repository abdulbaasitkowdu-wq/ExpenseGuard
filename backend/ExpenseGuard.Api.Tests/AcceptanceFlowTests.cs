using ExpenseGuard.Api.Contracts;
using ExpenseGuard.Api.Data;
using ExpenseGuard.Api.DTOs;
using ExpenseGuard.Api.Models;
using ExpenseGuard.Api.Reimbursements;
using ExpenseGuard.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExpenseGuard.Api.Tests;

public sealed class AcceptanceFlowTests
{
    [Fact]
    public async Task High_value_advertising_claim_runs_agents_three_approvals_then_pays()
    {
        await using var db = Database();
        var people = await SeedPeople(db);
        var policy = new PolicyService(db, TimeProvider.System);
        var fraud = new FraudService(db, TimeProvider.System);
        var budgets = new BudgetService(db);
        var ledger = new WorkflowLedger(db);
        var reimbursements = new ReimbursementService(db, new BudgetServiceGateway(db, budgets),
            new DeterministicPaymentProvider(), NullLogger<ReimbursementService>.Instance, ledger);
        var coordinator = new ClaimIntakeCoordinator(db, policy, fraud, budgets, reimbursements, ledger);
        var claims = new ClaimService(db, coordinator);

        await policy.CreateAsync(new CreatePolicyRequest
        {
            PolicyCode = "ADV-1",
            Category = "Advertising",
            MaxAmount = 50000,
            Currency = "USD",
            ReceiptRequired = true,
            EffectiveFrom = DateTime.UtcNow.AddYears(-1)
        }, default);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await budgets.AllocateAsync(new AllocateBudgetRequest
        {
            DepartmentId = people.DepartmentId,
            Name = "Marketing FY",
            PeriodStart = new DateOnly(today.Year, 1, 1),
            PeriodEnd = new DateOnly(today.Year, 12, 31),
            Currency = "USD",
            Amount = 100000
        }, default);

        var created = await claims.CreateAsync(new ClaimWriteDto
        {
            Amount = 15000,
            Category = "Advertising",
            Description = "Campaign ads",
            Currency = "USD",
            Vendor = "AdCo",
            Flow = ClaimFlow.OutOfPocket
        }, people.EmployeeId, default);
        db.Receipts.Add(new Receipt
        {
            ExpenseClaimId = created.ExpenseClaimId,
            StorageUrl = "https://files/receipt.pdf",
            PublicId = "r1",
            FileName = "receipt.pdf",
            ContentType = "application/pdf",
            Sha256 = "abc123",
            ExtractedAmount = 15000,
            ProcessingStatus = ReceiptProcessingStatus.Processed
        });
        await db.SaveChangesAsync();

        await claims.TransitionAsync(created.ExpenseClaimId, people.EmployeeId, ClaimStatus.Submitted, "submit", default);

        var claim = await db.ExpenseClaims.SingleAsync(c => c.ExpenseClaimId == created.ExpenseClaimId);
        var workflow = await db.WorkflowExecutions.Include(w => w.Steps).SingleAsync();
        var reimbursement = await db.Reimbursements.Include(r => r.ExpenseClaim).SingleAsync();
        var process = await db.ApprovalProcesses.Include(p => p.Steps).SingleAsync();
        var budget = await db.Budgets.SingleAsync();

        Assert.Equal(ClaimStatus.UnderReview, claim.Status);
        Assert.Equal("WAITING_FOR_APPROVAL", workflow.Status);
        Assert.Equal(["ReceiptExtraction", "PolicyCompliance", "FraudRisk", "BudgetMonitor", "HumanApproval"],
            workflow.Steps.OrderBy(s => s.Sequence).Select(s => s.Name).ToArray());
        Assert.Equal("compliant", (await db.PolicyEvaluations.SingleAsync()).Outcome);
        Assert.NotEqual("critical", (await db.FraudEvaluations.SingleAsync()).RiskLevel);
        Assert.Equal(15000, budget.ReservedAmount);
        Assert.Equal(ReimbursementStatuses.PendingApproval, reimbursement.Status);
        Assert.Equal([RoleNames.Manager, RoleNames.DepartmentHead, RoleNames.Finance],
            process.Steps.OrderBy(s => s.Sequence).Select(s => s.RequiredRole).ToArray());

        await reimbursements.DecideAsync(reimbursement.ReimbursementId, people.ManagerId,
            RoleNames.Manager, new ApprovalDecision("APPROVED", "ok"), default);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            reimbursements.DecideAsync(reimbursement.ReimbursementId, people.FinanceId,
                RoleNames.Finance, new ApprovalDecision("APPROVED", "too soon"), default));
        await reimbursements.DecideAsync(reimbursement.ReimbursementId, people.HeadId,
            RoleNames.DepartmentHead, new ApprovalDecision("APPROVED", "ok"), default);
        var approved = await reimbursements.DecideAsync(reimbursement.ReimbursementId, people.FinanceId,
            RoleNames.Finance, new ApprovalDecision("APPROVED", "ok"), default);

        Assert.Equal(ReimbursementStatuses.Approved, approved.Status);
        Assert.Equal(ClaimStatus.Approved, (await db.ExpenseClaims.SingleAsync()).Status);
        Assert.Equal("APPROVED", (await db.WorkflowExecutions.SingleAsync()).Status);
        Assert.Contains(db.AuditLogs, a => a.Action == "approval.approved");

        await reimbursements.ProcessAsync(reimbursement.ReimbursementId, default);
        var paid = await reimbursements.PayAsync(reimbursement.ReimbursementId, default);
        await db.Entry(budget).ReloadAsync();

        Assert.Equal(ReimbursementStatuses.Paid, paid.Status);
        Assert.Equal(0, budget.ReservedAmount);
        Assert.Equal(15000, budget.SpentAmount);
        Assert.Equal("COMPLETED", (await db.WorkflowExecutions.SingleAsync()).Status);
        Assert.Single(db.PaymentTransactions);
        Assert.Equal(paid.PaymentId, (await reimbursements.PayAsync(reimbursement.ReimbursementId, default)).PaymentId);
    }

    [Fact]
    public async Task Policy_failure_stops_before_approval()
    {
        await using var db = Database();
        var people = await SeedPeople(db);
        var policy = new PolicyService(db, TimeProvider.System);
        var fraud = new FraudService(db, TimeProvider.System);
        var budgets = new BudgetService(db);
        var ledger = new WorkflowLedger(db);
        var reimbursements = new ReimbursementService(db, new BudgetServiceGateway(db, budgets),
            new DeterministicPaymentProvider(), NullLogger<ReimbursementService>.Instance, ledger);
        var claims = new ClaimService(db, new ClaimIntakeCoordinator(db, policy, fraud, budgets, reimbursements, ledger));

        await policy.CreateAsync(new CreatePolicyRequest
        {
            PolicyCode = "ADV-CAP",
            Category = "Advertising",
            MaxAmount = 100,
            Currency = "USD",
            ReceiptRequired = true,
            EffectiveFrom = DateTime.UtcNow.AddYears(-1)
        }, default);

        var created = await claims.CreateAsync(new ClaimWriteDto
        {
            Amount = 15000, Category = "Advertising", Description = "Over cap", Currency = "USD"
        }, people.EmployeeId, default);
        db.Receipts.Add(new Receipt
        {
            ExpenseClaimId = created.ExpenseClaimId, StorageUrl = "x", PublicId = "x",
            FileName = "x.pdf", ContentType = "application/pdf", Sha256 = "hash"
        });
        await db.SaveChangesAsync();
        await claims.TransitionAsync(created.ExpenseClaimId, people.EmployeeId, ClaimStatus.Submitted, null, default);

        Assert.Equal(ClaimStatus.NeedsCorrection, (await db.ExpenseClaims.SingleAsync()).Status);
        Assert.Equal("FAILED", (await db.WorkflowExecutions.SingleAsync()).Status);
        Assert.Empty(db.Reimbursements);
        Assert.Empty(db.ApprovalProcesses);
    }

    private static AppDbContext Database()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static async Task<(int DepartmentId, int EmployeeId, int ManagerId, int HeadId, int FinanceId)> SeedPeople(AppDbContext db)
    {
        var department = new Department { Code = "MKT", DepartmentName = "Marketing" };
        var employeeRole = await db.Roles.SingleAsync(r => r.RoleName == RoleNames.Employee);
        var managerRole = await db.Roles.SingleAsync(r => r.RoleName == RoleNames.Manager);
        var headRole = await db.Roles.SingleAsync(r => r.RoleName == RoleNames.DepartmentHead);
        var financeRole = await db.Roles.SingleAsync(r => r.RoleName == RoleNames.Finance);
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
        var head = new Employee
        {
            Username = "hugo", NormalizedUsername = "HUGO", FullName = "Hugo", Email = "hugo@test.local",
            PasswordHash = "x", Department = department, Role = headRole
        };
        var finance = new Employee
        {
            Username = "fay", NormalizedUsername = "FAY", FullName = "Fay", Email = "fay@test.local",
            PasswordHash = "x", Department = department, Role = financeRole
        };
        db.AddRange(employee, manager, head, finance);
        await db.SaveChangesAsync();
        return (department.DepartmentId, employee.EmployeeId, manager.EmployeeId, head.EmployeeId, finance.EmployeeId);
    }
}

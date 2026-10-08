using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ExpenseGuard.Api.Auth;
using ExpenseGuard.Api.Data;
using ExpenseGuard.Api.Models;
using ExpenseGuard.Api.Reimbursements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ExpenseGuard.Api.Tests;

public sealed class FoundationTests
{
    [Fact]
    public void Bcrypt_login_material_and_jwt_roles_are_valid()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("long-test-password");
        Assert.True(BCrypt.Net.BCrypt.Verify("long-test-password", hash));
        Assert.False(BCrypt.Net.BCrypt.Verify("wrong-password", hash));
        var employee = new Employee
        {
            EmployeeId = 12, Username = "alice", DepartmentId = 3,
            Role = new Role { RoleName = RoleNames.Manager }
        };
        var service = new TokenService(Options.Create(new JwtOptions
        {
            SigningKey = "test-only-signing-key-longer-than-32-bytes",
            Issuer = "tests", Audience = "tests"
        }));
        var response = service.Issue(employee);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(response.Token);
        Assert.Contains(token.Claims, c => c.Type == ClaimTypes.Role && c.Value == RoleNames.Manager);
    }

    [Fact]
    public void Ownership_helper_enforces_employee_scope_and_privileged_roles()
    {
        var employee = Principal(5, RoleNames.Employee);
        var finance = Principal(9, RoleNames.Finance);
        Assert.True(employee.CanAccessEmployee(5));
        Assert.False(employee.CanAccessEmployee(6));
        Assert.True(finance.CanAccessEmployee(6));
    }

    [Fact]
    public async Task Approval_is_sequential_and_snapshots_are_immutable()
    {
        await using var db = Database();
        var data = await Seed(db);
        var service = Service(db);
        await service.StartApprovalAsync(data.reimbursement.ReimbursementId,
            data.template.ApprovalWorkflowTemplateId, default);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.DecideAsync(data.reimbursement.ReimbursementId, data.manager.EmployeeId,
                RoleNames.DepartmentHead, new ApprovalDecision("APPROVED", null), default));
        await service.DecideAsync(data.reimbursement.ReimbursementId, data.manager.EmployeeId,
            RoleNames.Manager, new ApprovalDecision("APPROVED", "stage one"), default);
        var process = await db.ApprovalProcesses.Include(p => p.Steps).SingleAsync();
        Assert.Equal(2, process.CurrentSequence);
        data.template.Name = "mutated later";
        await db.SaveChangesAsync();
        Assert.DoesNotContain("mutated later", process.TemplateSnapshotJson);

        var afterHead = await service.DecideAsync(data.reimbursement.ReimbursementId, data.head.EmployeeId,
            RoleNames.DepartmentHead, new ApprovalDecision("APPROVED", "stage two"), default);
        Assert.Equal(ReimbursementStatuses.PendingApproval, afterHead.Status);
        Assert.Equal(RoleNames.Finance, afterHead.CurrentRequiredRole);
        var result = await service.DecideAsync(data.reimbursement.ReimbursementId, data.head.EmployeeId,
            RoleNames.Admin, new ApprovalDecision("APPROVED", "finance"), default);
        Assert.Equal(ReimbursementStatuses.Approved, result.Status);
    }

    [Fact]
    public async Task Payment_is_idempotent()
    {
        await using var db = Database();
        var data = await Seed(db);
        data.reimbursement.Status = ReimbursementStatuses.Processing;
        await db.SaveChangesAsync();
        var payment = new CountingPayment();
        var service = Service(db, payment);
        var first = await service.PayAsync(data.reimbursement.ReimbursementId, default);
        var second = await service.PayAsync(data.reimbursement.ReimbursementId, default);
        Assert.Equal(ReimbursementStatuses.Paid, first.Status);
        Assert.Equal(first.PaymentId, second.PaymentId);
        Assert.Equal(1, payment.Calls);
        Assert.Single(db.PaymentTransactions);
    }

    [Fact]
    public async Task Manager_can_still_view_a_reimbursement_after_approving_their_stage()
    {
        await using var db = Database();
        var data = await Seed(db);
        var service = Service(db);
        await service.StartApprovalAsync(data.reimbursement.ReimbursementId,
            data.template.ApprovalWorkflowTemplateId, default);
        await service.DecideAsync(data.reimbursement.ReimbursementId, data.manager.EmployeeId,
            RoleNames.Manager, new ApprovalDecision("APPROVED", "ok"), default);

        var handler = new OwnsReimbursementHandler(db);
        var context = new AuthorizationHandlerContext(
            [new OwnsReimbursementRequirement()],
            Principal(data.manager.EmployeeId, RoleNames.Manager),
            data.reimbursement.ReimbursementId);
        await handler.HandleAsync(context);
        Assert.True(context.HasSucceeded);
    }

    private static ClaimsPrincipal Principal(int id, string role) => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, id.ToString()),
        new Claim(ClaimTypes.Role, role)
    }, "test"));

    private static AppDbContext Database()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static async Task<(Reimbursement reimbursement, ApprovalWorkflowTemplate template,
        Employee manager, Employee head)> Seed(AppDbContext db)
    {
        var department = new Department { DepartmentId = 1, DepartmentName = "Engineering" };
        var employeeRole = await db.Roles.SingleAsync(r => r.RoleName == RoleNames.Employee);
        var managerRole = await db.Roles.SingleAsync(r => r.RoleName == RoleNames.Manager);
        var headRole = await db.Roles.SingleAsync(r => r.RoleName == RoleNames.DepartmentHead);
        var owner = new Employee { EmployeeId = 10, Username = "owner", NormalizedUsername = "owner", FullName = "Owner", Email = "owner@test.local", PasswordHash = "x", Department = department, Role = employeeRole };
        var manager = new Employee { EmployeeId = 11, Username = "manager", NormalizedUsername = "manager", FullName = "Manager", Email = "manager@test.local", PasswordHash = "x", Department = department, Role = managerRole };
        var head = new Employee { EmployeeId = 12, Username = "head", NormalizedUsername = "head", FullName = "Head", Email = "head@test.local", PasswordHash = "x", Department = department, Role = headRole };
        var claim = new ExpenseClaim { ExpenseClaimId = 20, Employee = owner, Amount = 500, Category = "Travel", PurchaseNo = "P-1" };
        var reimbursement = new Reimbursement { ReimbursementId = 30, ExpenseClaim = claim, Total = 500, IdempotencyKey = "pay-30" };
        var template = new ApprovalWorkflowTemplate
        {
            Name = "Standard", Department = department,
            Stages =
            [
                new ApprovalStageDefinition { Sequence = 1, RequiredRole = RoleNames.Manager },
                new ApprovalStageDefinition { Sequence = 2, RequiredRole = RoleNames.DepartmentHead }
            ]
        };
        db.AddRange(owner, manager, head, reimbursement, template);
        await db.SaveChangesAsync();
        return (reimbursement, template, manager, head);
    }

    private static ReimbursementService Service(AppDbContext db, IPaymentProvider? payment = null) =>
        new(db, new AvailableBudget(), payment ?? new CountingPayment(), NullLogger<ReimbursementService>.Instance);

    private sealed class AvailableBudget : IBudgetGateway
    {
        public Task<BudgetCheckResult> CheckAsync(int departmentId, decimal amount, CancellationToken ct) =>
            Task.FromResult(new BudgetCheckResult(true));
        public Task RecordPaymentAsync(int departmentId, decimal amount, int reimbursementId, CancellationToken ct) =>
            Task.CompletedTask;
        public Task ReleaseReservationAsync(int departmentId, decimal amount, int reimbursementId, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private sealed class CountingPayment : IPaymentProvider
    {
        public int Calls { get; private set; }
        public Task<PaymentResponse> PayAsync(PaymentRequest request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new PaymentResponse(true, "COMPLETED", "txn-1"));
        }
    }
}

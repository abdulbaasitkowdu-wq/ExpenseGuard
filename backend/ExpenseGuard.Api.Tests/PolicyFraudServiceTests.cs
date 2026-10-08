using ExpenseGuard.Api.Contracts;
using ExpenseGuard.Api.Controllers;
using ExpenseGuard.Api.Data;
using ExpenseGuard.Api.Models;
using ExpenseGuard.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExpenseGuard.Api.Tests;

public sealed class PolicyFraudServiceTests
{
    [Fact]
    public async Task Policy_precedence_honors_department_priority_and_effective_date()
    {
        await using var db = CreateDb();
        SeedClaim(db, amount: 120, receipt: true);
        db.Policies.AddRange(
            Policy("GLOBAL", null, 50, 100, DateTime.UtcNow.AddDays(-2)),
            Policy("DEPT-EXPIRED", 7, 100, 200, DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddDays(-1)),
            Policy("DEPT", 7, 1, 150, DateTime.UtcNow.AddDays(-2)));
        await db.SaveChangesAsync();

        var result = await new PolicyService(db, TimeProvider.System).EvaluateAsync(
            new() { ExpenseClaimId = 1, Currency = "USD" }, default);

        Assert.NotNull(result);
        Assert.Equal("compliant", result.Outcome);
        Assert.Equal("DEPT", (await db.Policies.FindAsync(result.PolicyId))!.PolicyCode);
    }

    [Fact]
    public async Task Policy_evaluation_persists_receipt_and_cap_violations()
    {
        await using var db = CreateDb();
        SeedClaim(db, amount: 250, receipt: false);
        db.Policies.Add(Policy("TRAVEL", 7, 1, 100, DateTime.UtcNow.AddDays(-1), receiptRequired: true));
        await db.SaveChangesAsync();

        var result = await new PolicyService(db, TimeProvider.System).EvaluateAsync(
            new() { ExpenseClaimId = 1, Currency = "USD" }, default);

        Assert.Equal("non_compliant", result!.Outcome);
        Assert.Equal(["POLICY_CAP_EXCEEDED", "RECEIPT_REQUIRED"], result.Violations.Select(x => x.RuleCode).Order().ToArray());
        Assert.Equal(2, await db.PolicyViolations.CountAsync());
    }

    [Fact]
    public async Task Fraud_rules_are_deterministic_and_idempotent()
    {
        await using var db = CreateDb();
        SeedClaim(db, amount: 500, receipt: false, purchaseNo: "R-1");
        db.ExpenseClaims.Add(new ExpenseClaim
        {
            ExpenseClaimId = 2, EmployeeId = 1, Amount = 10, Category = "Travel",
            PurchaseNo = "R-1", ReceiptDoc = "receipt.pdf"
        });
        await db.SaveChangesAsync();
        var service = new FraudService(db, TimeProvider.System);
        await service.EvaluateAsync(new() { ExpenseClaimId = 2, InvoiceNumber = "INV-9" }, default);
        var request = new FraudEvaluateRequest { ExpenseClaimId = 1, ReceiptAmount = 450, InvoiceNumber = "inv-9" };

        var first = await service.EvaluateAsync(request, default);
        var second = await service.EvaluateAsync(request, default);

        Assert.Equal(first!.EvaluationId, second!.EvaluationId);
        Assert.Contains(first.Flags, x => x.RuleCode == "MISSING_RECEIPT");
        Assert.Contains(first.Flags, x => x.RuleCode == "DUPLICATE_RECEIPT");
        Assert.Contains(first.Flags, x => x.RuleCode == "DUPLICATE_INVOICE");
        Assert.Contains(first.Flags, x => x.RuleCode == "AMOUNT_MISMATCH");
        Assert.Equal(2, await db.FraudEvaluations.CountAsync());
    }

    [Fact]
    public async Task Missing_claim_fails_safely_without_writes()
    {
        await using var db = CreateDb();
        var result = await new FraudService(db, TimeProvider.System)
            .EvaluateAsync(new() { ExpenseClaimId = 999 }, default);
        Assert.Null(result);
        Assert.Empty(db.FraudEvaluations);
    }

    [Fact]
    public void Resolve_endpoint_requires_privileged_role()
    {
        var method = typeof(FraudController).GetMethod(nameof(FraudController.Resolve))!;
        var roles = method.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single().Roles;
        Assert.Equal("Admin,FraudAnalyst", roles);
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static void SeedClaim(AppDbContext db, decimal amount, bool receipt, string purchaseNo = "P-1")
    {
        var department = new Department { DepartmentId = 7, DepartmentName = "Engineering" };
        var role = new Role { RoleId = 1, RoleName = "Employee" };
        var employee = new Employee { EmployeeId = 1, Username = "u", PasswordHash = "x", DepartmentId = 7, RoleId = 1, Department = department, Role = role };
        db.Add(new ExpenseClaim
        {
            ExpenseClaimId = 1, Employee = employee, EmployeeId = 1, Amount = amount,
            Category = "Travel", PurchaseNo = purchaseNo, ReceiptDoc = receipt ? "receipt.pdf" : null
        });
    }

    private static Policy Policy(string code, int? departmentId, int priority, decimal cap, DateTime from,
        DateTime? to = null, bool receiptRequired = false) => new()
    {
        PolicyCode = code, Version = 1, Category = "Travel", Currency = "USD", MaxAmount = cap,
        DepartmentId = departmentId, Priority = priority, EffectiveFrom = from, EffectiveTo = to,
        ReceiptRequired = receiptRequired, IsActive = true
    };
}

using ExpenseGuard.Api.Data;
using ExpenseGuard.Api.Models;
using ExpenseGuard.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExpenseGuard.Api.Tests;

public sealed class DemoDataSeederTests
{
    [Fact]
    public async Task Northstar_seed_replaces_demo_data_with_company_org_policies_and_lkr_budgets()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Database.EnsureCreated();

        await DemoDataSeeder.SeedAsync(db);
        await DemoDataSeeder.SeedAsync(db);

        Assert.Equal(29, await db.Employees.CountAsync());
        Assert.Equal(7, await db.Departments.CountAsync());
        Assert.Equal(28, await db.Budgets.CountAsync());
        Assert.True(await db.Policies.CountAsync() > 40);
        Assert.True(BCrypt.Net.BCrypt.Verify(DemoDataSeeder.Password,
            (await db.Employees.SingleAsync(e => e.Username == "nimal.perera")).PasswordHash));

        var ceo = await db.Employees.Include(e => e.Role).Include(e => e.Department)
            .SingleAsync(e => e.Username == "daniel.perera");
        Assert.Equal(RoleNames.Admin, ceo.Role.RoleName);
        Assert.Equal("Executive", ceo.Department.DepartmentName);

        var engineer = await db.Employees.Include(e => e.Role).Include(e => e.Department)
            .SingleAsync(e => e.Username == "nimal.perera");
        Assert.Equal(RoleNames.Employee, engineer.Role.RoleName);
        Assert.Equal("Engineering & IT", engineer.Department.DepartmentName);

        var director = await db.Employees.Include(e => e.Role)
            .SingleAsync(e => e.Username == "alex.wijesinghe");
        Assert.Equal(RoleNames.DepartmentHead, director.Role.RoleName);

        var financeManager = await db.Employees.Include(e => e.Role)
            .SingleAsync(e => e.Username == "priyantha.silva");
        Assert.Equal(RoleNames.Finance, financeManager.Role.RoleName);

        Assert.False(await db.Employees.AnyAsync(e => e.Username == "sarah.fernando" && e.Department.Code == "FIN"));
        Assert.Equal(3, await db.Employees.CountAsync(e => e.Department.Code == "FIN"));
        Assert.Equal(1, await db.Employees.CountAsync(e => e.Email == "sarah.fernando@northstar.lk"));

        Assert.All(await db.Budgets.ToListAsync(), budget =>
        {
            Assert.Equal("LKR", budget.Currency);
            Assert.Equal(2026, budget.PeriodStart.Year);
        });
        var engQ1 = await db.Budgets.SingleAsync(b => b.Name == DemoDataSeeder.BudgetForName(
            "Product engineering, cloud and tooling", "Q1", 2026));
        Assert.Equal(DemoDataSeeder.ScaledQuarterLkr(180_000, 190_000, 200_000, 190_000)[0], engQ1.AllocatedAmount);
        Assert.All((await db.Budgets.ToListAsync()).GroupBy(b => new { b.DepartmentId, b.Currency }),
            group => Assert.True(group.Sum(b => b.AllocatedAmount) <= BudgetService.MaxDepartmentBudget));

        Assert.Contains(await db.Policies.ToListAsync(), p => p.Category == "Gambling" && p.MaxAmount == 0);
        Assert.Contains(await db.Policies.ToListAsync(), p => p.Category == "GitHub" && p.Currency == "LKR");

        var template = await db.ApprovalWorkflowTemplates.Include(t => t.Stages).SingleAsync();
        Assert.Equal(ClaimIntakeCoordinator.HighValueTemplateName, template.Name);
        Assert.Equal(3, template.Stages.Count);
        Assert.Null(template.Stages.Single(s => s.RequiredRole == RoleNames.Finance).MinimumAmount);
    }
}

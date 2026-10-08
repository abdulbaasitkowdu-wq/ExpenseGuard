using ExpenseGuard.Api.Data;
using ExpenseGuard.Api.DTOs;
using ExpenseGuard.Api.Models;
using ExpenseGuard.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExpenseGuard.Api.Tests;

public sealed class BudgetServiceTests
{
    [Fact]
    public void Arithmetic_tracks_reserved_spent_and_available()
    {
        var budget = NewBudget(1000m);

        budget.Reserve(300m);
        budget.Spend(125m, fromReservation: true);
        budget.Release(75m);

        Assert.Equal(100m, budget.ReservedAmount);
        Assert.Equal(125m, budget.SpentAmount);
        Assert.Equal(775m, budget.AvailableAmount);
    }

    [Fact]
    public void Reserve_rejects_insufficient_funds()
    {
        var budget = NewBudget(100m);
        Assert.Throws<InsufficientBudgetException>(() => budget.Reserve(100.01m));
    }

    [Fact]
    public async Task Mutation_is_idempotent_and_rejects_stale_version()
    {
        await using var db = CreateDb();
        var department = new Department { Code = "ENG", DepartmentName = "Engineering" };
        var budget = NewBudget(500m);
        budget.Department = department;
        db.AddRange(department, budget);
        await db.SaveChangesAsync();
        var service = new BudgetService(db);
        var originalVersion = budget.Version;
        var request = new BudgetAmountRequest { Amount = 100m, IdempotencyKey = "reserve-1", Version = originalVersion };

        var first = await service.ReserveAsync(budget.BudgetId, request, default);
        var replay = await service.ReserveAsync(budget.BudgetId, request, default);

        Assert.Equal(100m, first!.ReservedAmount);
        Assert.Equal(100m, replay!.ReservedAmount);
        Assert.Single(db.BudgetTransactions);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            service.ReserveAsync(budget.BudgetId,
                new BudgetAmountRequest { Amount = 1m, IdempotencyKey = "reserve-2", Version = originalVersion }, default));
    }

    [Fact]
    public async Task Department_code_is_unique_and_in_use_department_cannot_be_deleted()
    {
        await using var db = CreateDb();
        var service = new DepartmentService(db);
        var created = await service.CreateAsync(new CreateDepartmentRequest { Code = "FIN", Name = "Finance" }, default);
        await Assert.ThrowsAsync<DuplicateResourceException>(() =>
            service.CreateAsync(new CreateDepartmentRequest { Code = "fin", Name = "Other" }, default));

        db.Budgets.Add(new Budget
        {
            DepartmentId = created.Id, Name = "FY", PeriodStart = new(2026, 1, 1),
            PeriodEnd = new(2026, 12, 31), AllocatedAmount = 10m
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidBudgetOperationException>(() => service.DeleteAsync(created.Id, default));
    }

    [Fact]
    public async Task Allocate_rejects_when_department_total_exceeds_one_hundred_million()
    {
        await using var db = CreateDb();
        var department = new Department { Code = "ENG", DepartmentName = "Engineering" };
        db.Add(department);
        await db.SaveChangesAsync();
        var service = new BudgetService(db);

        await service.AllocateAsync(new AllocateBudgetRequest
        {
            DepartmentId = department.DepartmentId, Name = "Cloud", PeriodStart = new(2026, 1, 1),
            PeriodEnd = new(2026, 3, 31), Currency = "LKR", Amount = 60_000_000m
        }, default);

        await Assert.ThrowsAsync<InvalidBudgetOperationException>(() => service.AllocateAsync(new AllocateBudgetRequest
        {
            DepartmentId = department.DepartmentId, Name = "Tooling", PeriodStart = new(2026, 4, 1),
            PeriodEnd = new(2026, 6, 30), Currency = "LKR", Amount = 50_000_000m
        }, default));
    }

    [Fact]
    public async Task Utilization_includes_what_the_budget_is_for()
    {
        await using var db = CreateDb();
        var department = new Department { Code = "ENG", DepartmentName = "Engineering" };
        var budget = NewBudget(1_000m);
        budget.Name = "Product engineering, cloud and tooling — Q1 2026";
        budget.Department = department;
        db.AddRange(department, budget);
        await db.SaveChangesAsync();

        var page = await new BudgetService(db).UtilizationAsync(1, 10, null, default);

        Assert.Equal("Engineering", page.Items[0].DepartmentName);
        Assert.Equal("Product engineering, cloud and tooling — Q1 2026", page.Items[0].BudgetName);
    }

    private static Budget NewBudget(decimal allocated) => new()
    {
        Name = "Operations", PeriodStart = new(2026, 1, 1), PeriodEnd = new(2026, 12, 31),
        Currency = "LKR", AllocatedAmount = allocated
    };

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new AppDbContext(options);
    }
}

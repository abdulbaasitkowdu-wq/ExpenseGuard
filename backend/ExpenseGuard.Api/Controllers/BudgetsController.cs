using ExpenseGuard.Api.DTOs;
using ExpenseGuard.Api.Models;
using ExpenseGuard.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseGuard.Api.Controllers;

[ApiController]
[Route("api/budgets")]
[Authorize(Roles = "Admin,Finance,Manager")]
public sealed class BudgetsController(IBudgetService service) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Admin,Finance")]
    public async Task<ActionResult<BudgetDto>> Allocate(AllocateBudgetRequest request, CancellationToken ct)
    {
        var budget = await service.AllocateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = budget.Id }, budget);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BudgetDto>> Get(int id, CancellationToken ct)
    {
        var budget = await service.GetAsync(id, ct);
        return budget is null ? NotFound() : Ok(budget);
    }

    [HttpGet("{id:int}/availability")]
    public async Task<ActionResult<AvailabilityDto>> Availability(
        int id, [FromQuery] decimal amount, CancellationToken ct)
    {
        var result = await service.CheckAvailabilityAsync(id, amount, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:int}/reserve")]
    [Authorize(Roles = "Admin,Finance,Manager")]
    public Task<ActionResult<BudgetDto>> Reserve(int id, BudgetAmountRequest request, CancellationToken ct) =>
        Mutate(service.ReserveAsync(id, request, ct));

    [HttpPost("{id:int}/release")]
    [Authorize(Roles = "Admin,Finance")]
    public Task<ActionResult<BudgetDto>> Release(int id, BudgetAmountRequest request, CancellationToken ct) =>
        Mutate(service.ReleaseAsync(id, request, ct));

    [HttpPost("{id:int}/spend")]
    [Authorize(Roles = "Admin,Finance")]
    public Task<ActionResult<BudgetDto>> Spend(int id, SpendBudgetRequest request, CancellationToken ct) =>
        Mutate(service.SpendAsync(id, request, ct));

    [HttpGet("{id:int}/transactions")]
    public async Task<ActionResult<PagedResult<BudgetTransactionDto>>> Transactions(
        int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await service.TransactionsAsync(id, Math.Max(page, 1), Math.Clamp(pageSize, 1, 100), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:int}/alerts")]
    public async Task<ActionResult<IReadOnlyList<BudgetAlertDto>>> Alerts(
        int id, [FromQuery] BudgetAlertStatus? status, CancellationToken ct)
    {
        var result = await service.AlertsAsync(id, status, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("reports/utilization")]
    public async Task<ActionResult<PagedResult<UtilizationDto>>> Utilization(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] int? departmentId = null, CancellationToken ct = default) =>
        Ok(await service.UtilizationAsync(Math.Max(page, 1), Math.Clamp(pageSize, 1, 100), departmentId, ct));

    private static async Task<ActionResult<BudgetDto>> Mutate(Task<BudgetDto?> operation)
    {
        var result = await operation;
        return result is null ? new NotFoundResult() : new OkObjectResult(result);
    }
}

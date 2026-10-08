using ExpenseGuard.Api.DTOs;
using ExpenseGuard.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseGuard.Api.Controllers;

[ApiController]
[Route("api/departments")]
[Authorize(Roles = "Admin,Finance,Manager")]
public sealed class DepartmentsController(IDepartmentService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<DepartmentDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool? active = null,
        CancellationToken ct = default) =>
        Ok(await service.ListAsync(Math.Max(page, 1), Math.Clamp(pageSize, 1, 100), active, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DepartmentDto>> Get(int id, CancellationToken ct)
    {
        var item = await service.GetAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DepartmentDto>> Create(CreateDepartmentRequest request, CancellationToken ct)
    {
        var item = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DepartmentDto>> Update(
        int id, UpdateDepartmentRequest request, CancellationToken ct)
    {
        var item = await service.UpdateAsync(id, request, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        await service.DeleteAsync(id, ct) ? NoContent() : NotFound();
}

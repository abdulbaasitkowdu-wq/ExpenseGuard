using System.Security.Claims;
using ExpenseGuard.Api.Auth;
using ExpenseGuard.Api.Models;
using ExpenseGuard.Api.Reimbursements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseGuard.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reimbursements")]
public sealed class ReimbursementsController(IReimbursementService service, IAuthorizationService authorization) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = RoleNames.Employee + "," + RoleNames.Admin)]
    public async Task<ActionResult<ReimbursementDto>> Create(CreateReimbursementRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, User.EmployeeId(), ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpGet("employee/{employeeId:int}")]
    public async Task<ActionResult<IReadOnlyList<ReimbursementDto>>> Employee(int employeeId, CancellationToken ct)
    {
        if (!User.CanAccessEmployee(employeeId)) return Forbid();
        return Ok(await service.ListForEmployeeAsync(employeeId, ct));
    }

    [HttpGet("finance-queue")]
    [Authorize(Policy = "FinanceOnly")]
    public async Task<ActionResult<IReadOnlyList<ReimbursementDto>>> FinanceQueue([FromQuery] string? status, CancellationToken ct) =>
        Ok(await service.FinanceQueueAsync(status, ct));

    [HttpGet("approval-queue")]
    [Authorize(Policy = "CanApprove")]
    public async Task<ActionResult<IReadOnlyList<ReimbursementDto>>> ApprovalQueue(CancellationToken ct) =>
        Ok(await service.ApprovalQueueAsync(
            User.FindFirstValue(ClaimTypes.Role) ?? string.Empty, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReimbursementDto>> Get(int id, CancellationToken ct)
    {
        var allowed = await authorization.AuthorizeAsync(User, id, "OwnReimbursement");
        if (!allowed.Succeeded) return Forbid();
        var result = await service.GetAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:int}/approval-process")]
    [Authorize(Policy = "CanApprove")]
    public async Task<ActionResult<ReimbursementDto>> Start(int id, [FromQuery] int templateId, CancellationToken ct) =>
        Ok(await service.StartApprovalAsync(id, templateId, ct));

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = "CanApprove")]
    public Task<ActionResult<ReimbursementDto>> Approve(int id, [FromBody] string? comment, CancellationToken ct) =>
        Decide(id, ApprovalStatuses.Approved, comment, ct);

    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = "CanApprove")]
    public Task<ActionResult<ReimbursementDto>> Reject(int id, [FromBody] string? comment, CancellationToken ct) =>
        Decide(id, ApprovalStatuses.Rejected, comment, ct);

    [HttpPost("{id:int}/revise")]
    [Authorize(Policy = "CanApprove")]
    public Task<ActionResult<ReimbursementDto>> Revise(int id, [FromBody] string? comment, CancellationToken ct) =>
        Decide(id, ApprovalStatuses.RevisionRequired, comment, ct);

    [HttpPost("{id:int}/process")]
    [Authorize(Policy = "FinanceOnly")]
    public async Task<ActionResult<ReimbursementDto>> Process(int id, CancellationToken ct) =>
        Ok(await service.ProcessAsync(id, ct));

    [HttpPost("{id:int}/payment")]
    [Authorize(Policy = "FinanceOnly")]
    public async Task<ActionResult<ReimbursementDto>> Pay(int id, CancellationToken ct) =>
        Ok(await service.PayAsync(id, ct));

    private async Task<ActionResult<ReimbursementDto>> Decide(int id, string decision, string? comment, CancellationToken ct)
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        return Ok(await service.DecideAsync(id, User.EmployeeId(), role, new ApprovalDecision(decision, comment), ct));
    }
}

using System.Security.Claims;
using ExpenseGuard.Api.Auth;
using ExpenseGuard.Api.Contracts;
using ExpenseGuard.Api.Models;
using ExpenseGuard.Api.Reimbursements;
using ExpenseGuard.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseGuard.Api.Controllers;

[ApiController]
[Route("api/employees")]
public sealed class EmployeesController(IEmployeeService employees, ICurrentEmployee actor) : ControllerBase
{
    [HttpGet("me")]
    public Task<EmployeeProfileDto> Profile(CancellationToken ct) => employees.GetProfileAsync(actor.EmployeeId, ct);

    [HttpGet]
    public Task<IReadOnlyList<EmployeeProfileDto>> List(CancellationToken ct) => employees.ListAsync(actor.EmployeeId, ct);
}

[ApiController]
[Route("api/purchase-requests")]
public sealed class PurchaseRequestsController(IPurchaseRequestService requests, ICurrentEmployee actor) : ControllerBase
{
    [HttpGet] public Task<IReadOnlyList<PurchaseRequestDto>> List(CancellationToken ct) => requests.ListAsync(actor.EmployeeId, ct);

    [HttpGet("approval-queue")]
    [Authorize(Policy = "CanApprove")]
    public Task<IReadOnlyList<PurchaseRequestDto>> ApprovalQueue(CancellationToken ct) =>
        requests.ApprovalQueueAsync(User.FindFirstValue(ClaimTypes.Role) ?? string.Empty, ct);

    [HttpGet("{id:int}")]
    public Task<PurchaseRequestDto> Get(int id, CancellationToken ct)
        => requests.GetForApprovalAsync(id, actor.EmployeeId, User.FindFirstValue(ClaimTypes.Role) ?? string.Empty, ct);

    [HttpGet("{id:int}/history")]
    public Task<IReadOnlyList<PurchaseRequestHistoryDto>> History(int id, CancellationToken ct)
        => requests.HistoryAsync(id, actor.EmployeeId, User.FindFirstValue(ClaimTypes.Role) ?? string.Empty, ct);

    [HttpPost]
    public async Task<ActionResult<PurchaseRequestDto>> Create(PurchaseRequestWriteDto input, CancellationToken ct)
    {
        var result = await requests.CreateAsync(input, actor.EmployeeId, ct);
        return CreatedAtAction(nameof(Get), new { id = result.PurchaseRequestId }, result);
    }

    [HttpPut("{id:int}")]
    public Task<PurchaseRequestDto> Update(int id, PurchaseRequestWriteDto input, CancellationToken ct)
        => requests.UpdateAsync(id, input, actor.EmployeeId, ct);

    [HttpPost("{id:int}/submit")]
    public async Task<IActionResult> Submit(int id, CancellationToken ct)
    {
        await requests.SubmitAsync(id, actor.EmployeeId, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = "CanApprove")]
    public Task<PurchaseRequestDto> Approve(int id, [FromBody] string? comment, CancellationToken ct) =>
        Decide(id, ApprovalStatuses.Approved, comment, ct);

    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = "CanApprove")]
    public Task<PurchaseRequestDto> Reject(int id, [FromBody] string? comment, CancellationToken ct) =>
        Decide(id, ApprovalStatuses.Rejected, comment, ct);

    [HttpPost("{id:int}/revise")]
    [Authorize(Policy = "CanApprove")]
    public Task<PurchaseRequestDto> Revise(int id, [FromBody] string? comment, CancellationToken ct) =>
        Decide(id, ApprovalStatuses.RevisionRequired, comment, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await requests.DeleteAsync(id, actor.EmployeeId, ct);
        return NoContent();
    }

    private Task<PurchaseRequestDto> Decide(int id, string decision, string? comment, CancellationToken ct) =>
        requests.DecideAsync(id, actor.EmployeeId, User.FindFirstValue(ClaimTypes.Role) ?? string.Empty,
            new ApprovalDecision(decision, comment), ct);
}

[ApiController]
[Route("api/claims")]
public sealed class ClaimsController(IClaimService claims, IReceiptService receipts, ICurrentEmployee actor) : ControllerBase
{
    [HttpGet] public Task<IReadOnlyList<ClaimDto>> Search([FromQuery] ClaimSearchQuery query, CancellationToken ct)
        => claims.SearchAsync(query, actor.EmployeeId, ct);
    [HttpGet("{id:int}")] public Task<ClaimDto> Get(int id, CancellationToken ct) => claims.GetAsync(id, actor.EmployeeId, ct);

    [HttpPost]
    public async Task<ActionResult<ClaimDto>> Create(ClaimWriteDto input, CancellationToken ct)
    {
        var result = await claims.CreateAsync(input, actor.EmployeeId, ct);
        return CreatedAtAction(nameof(Get), new { id = result.ExpenseClaimId }, result);
    }

    [HttpPut("{id:int}")]
    public Task<ClaimDto> Update(int id, ClaimWriteDto input, CancellationToken ct)
        => claims.UpdateAsync(id, input, actor.EmployeeId, ct);

    [HttpPost("{id:int}/submit")]
    public async Task<IActionResult> Submit(int id, CancellationToken ct)
    {
        await claims.TransitionAsync(id, actor.EmployeeId, ClaimStatus.Submitted, null, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/resubmit")]
    public async Task<IActionResult> Resubmit(int id, [FromBody] string? reason, CancellationToken ct)
    {
        await claims.TransitionAsync(id, actor.EmployeeId, ClaimStatus.Submitted, reason, ct);
        return NoContent();
    }

    [HttpGet("{id:int}/history")]
    public Task<IReadOnlyList<ClaimHistoryDto>> History(int id, CancellationToken ct)
        => claims.HistoryAsync(id, actor.EmployeeId, User.FindFirstValue(ClaimTypes.Role) ?? string.Empty, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await claims.DeleteAsync(id, actor.EmployeeId, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/receipts")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ReceiptDto>> UploadReceipt(int id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var result = await receipts.UploadAsync(id, stream, file.FileName, file.ContentType, file.Length, actor.EmployeeId, ct);
        return Created($"api/claims/{id}/receipts/{result.ReceiptId}", result);
    }

    [HttpPatch("{id:int}/receipts/{receiptId:int}")]
    public Task<ReceiptDto> CorrectReceipt(int id, int receiptId, ReceiptCorrectionDto input, CancellationToken ct)
        => receipts.CorrectAsync(id, receiptId, input, actor.EmployeeId, ct);
}

[ApiController]
[Route("api/request-history")]
[Authorize(Policy = "FinanceOnly")]
public sealed class RequestHistoryController(IRequestHistoryService history) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<RequestHistoryDto>> List([FromQuery] RequestHistoryQuery query, CancellationToken ct)
        => history.ListAsync(query, ct);
}

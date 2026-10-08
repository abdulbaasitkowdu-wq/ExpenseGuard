using System.Security.Claims;
using ExpenseGuard.Api.Contracts;
using ExpenseGuard.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseGuard.Api.Controllers;

[ApiController]
[Route("api/fraud")]
[Authorize]
public sealed class FraudController(IFraudService service) : ControllerBase
{
    [HttpPost("evaluate")]
    [Authorize(Roles = "Admin,FraudAnalyst,Finance,Manager")]
    public async Task<ActionResult<FraudEvaluationDto>> Evaluate(FraudEvaluateRequest request, CancellationToken cancellationToken)
    {
        var result = await service.EvaluateAsync(request, cancellationToken);
        return result is null ? NotFound(new { message = "Expense claim not found." }) : Ok(result);
    }

    [HttpGet("flags")]
    [Authorize(Roles = "Admin,FraudAnalyst,Finance,Auditor")]
    public async Task<ActionResult<PageResult<FraudFlagDto>>> List(
        [FromQuery] string? status, [FromQuery] string? severity, [FromQuery] int? claimId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default) =>
        Ok(await service.ListAsync(status, severity, claimId, page, pageSize, cancellationToken));

    [HttpPatch("flags/{id:int}/review")]
    [Authorize(Roles = "Admin,FraudAnalyst")]
    public async Task<ActionResult<FraudFlagDto>> Review(int id, ReviewFraudFlagRequest request, CancellationToken cancellationToken)
    {
        var result = await service.ReviewAsync(id, request, Actor(), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("flags/{id:int}/resolve")]
    [Authorize(Roles = "Admin,FraudAnalyst")]
    public async Task<ActionResult<FraudFlagDto>> Resolve(int id, ResolveFraudFlagRequest request, CancellationToken cancellationToken)
    {
        var result = await service.ResolveAsync(id, request, Actor(), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    private string Actor() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.Identity?.Name
        ?? throw new InvalidOperationException("Authenticated actor identifier is required.");
}

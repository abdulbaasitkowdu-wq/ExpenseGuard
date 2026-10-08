using ExpenseGuard.Api.Contracts;
using ExpenseGuard.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseGuard.Api.Controllers;

[ApiController]
[Route("api/policies")]
[Authorize]
public sealed class PoliciesController(IPolicyService service) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Admin,PolicyManager")]
    public async Task<ActionResult<PolicyDto>> Create(CreatePolicyRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), new { policyCode = result.PolicyCode }, result);
    }

    [HttpPost("{policyId:int}/versions")]
    [Authorize(Roles = "Admin,PolicyManager")]
    public async Task<ActionResult<PolicyDto>> CreateVersion(int policyId, CreatePolicyVersionRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateVersionAsync(policyId, request, cancellationToken);
        return result is null ? NotFound() : CreatedAtAction(nameof(List), new { policyCode = result.PolicyCode }, result);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,PolicyManager,Finance,Manager,DepartmentHead,Auditor")]
    public async Task<ActionResult<PageResult<PolicyDto>>> List(
        [FromQuery] string? category, [FromQuery] int? departmentId, [FromQuery] bool? active,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default) =>
        Ok(await service.ListAsync(category, departmentId, active, page, pageSize, cancellationToken));

    [HttpPost("evaluate")]
    [Authorize(Roles = "Admin,PolicyManager,Finance,Manager,DepartmentHead,Employee,Auditor")]
    public async Task<ActionResult<PolicyEvaluationDto>> Evaluate(PolicyEvaluateRequest request, CancellationToken cancellationToken)
    {
        var result = await service.EvaluateAsync(request, cancellationToken);
        return result is null ? NotFound(new { message = "Expense claim not found." }) : Ok(result);
    }
}

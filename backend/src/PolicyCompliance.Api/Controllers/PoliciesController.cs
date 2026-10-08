using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PolicyCompliance.Core.DTOs;
using PolicyCompliance.Core.Services;

namespace PolicyCompliance.Api.Controllers;

[ApiController]
[Route("api/policy-compliance/policies")]
public class PoliciesController : ControllerBase
{
    private readonly IPolicyManagementService _policyService;

    public PoliciesController(IPolicyManagementService policyService)
    {
        _policyService = policyService;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetPolicies(CancellationToken cancellationToken)
    {
        var policies = await _policyService.GetPoliciesAsync(cancellationToken);
        return Ok(policies);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetPolicyById(Guid id, CancellationToken cancellationToken)
    {
        var policy = await _policyService.GetPolicyByIdAsync(id, cancellationToken);
        if (policy == null) return NotFound();
        return Ok(policy);
    }

    [HttpPost]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> CreatePolicy([FromBody] CreateExpensePolicyDto dto, CancellationToken cancellationToken)
    {
        var created = await _policyService.CreatePolicyAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetPolicyById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> UpdatePolicy(Guid id, [FromBody] CreateExpensePolicyDto dto, CancellationToken cancellationToken)
    {
        var updated = await _policyService.UpdatePolicyAsync(id, dto, cancellationToken);
        if (updated == null) return NotFound();
        return Ok(updated);
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PolicyCompliance.Core.DTOs;
using PolicyCompliance.Core.Services;

namespace PolicyCompliance.Api.Controllers;

[ApiController]
[Route("api/policy-compliance")]
public class PolicyComplianceController : ControllerBase
{
    private readonly IPolicyComplianceWorkflowService _workflowService;

    public PolicyComplianceController(IPolicyComplianceWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    private Guid GetAuthenticatedUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var id))
            throw new UnauthorizedAccessException("Valid user identity claim missing in token.");
        return id;
    }

    // 1. Dashboard Overview Stats (Manager, Finance, Admin)
    [HttpGet("stats/compliance-dashboard")]
    [Authorize(Roles = "Manager,Finance,Admin")]
    public async Task<IActionResult> GetDashboardStats(CancellationToken cancellationToken)
    {
        var stats = await _workflowService.GetDashboardStatsAsync(cancellationToken);
        return Ok(stats);
    }

    // 2. Manager Review Queue (Section 13)
    [HttpGet("review-queue")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> GetReviewQueue([FromQuery] ReviewQueueFilterDto filter, CancellationToken cancellationToken)
    {
        var queue = await _workflowService.GetReviewQueueAsync(filter, cancellationToken);
        return Ok(queue);
    }

    // 3. Claim Details (Section 13)
    [HttpGet("claims/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetClaimById(Guid id, CancellationToken cancellationToken)
    {
        var claim = await _workflowService.GetClaimDetailsAsync(id, cancellationToken);
        if (claim == null)
            return NotFound(new { message = $"Expense claim {id} not found." });

        return Ok(claim);
    }

    // 4. Claim Risk Details (Section 13)
    [HttpGet("claims/{id:guid}/risk")]
    [Authorize(Roles = "Manager,Finance,Admin")]
    public async Task<IActionResult> GetClaimRisk(Guid id, CancellationToken cancellationToken)
    {
        var claim = await _workflowService.GetClaimDetailsAsync(id, cancellationToken);
        if (claim == null)
            return NotFound(new { message = $"Expense claim {id} not found." });

        return Ok(new
        {
            claimId = claim.Id,
            claimNumber = claim.ClaimNumber,
            riskScore = claim.RiskScore,
            riskStatus = claim.RiskStatus,
            riskAssessment = claim.RiskAssessment,
            duplicateMatches = claim.DuplicateMatches
        });
    }

    // 5. Claim Policy Checks (Section 13)
    [HttpGet("claims/{id:guid}/policy-checks")]
    [Authorize]
    public async Task<IActionResult> GetClaimPolicyChecks(Guid id, CancellationToken cancellationToken)
    {
        var claim = await _workflowService.GetClaimDetailsAsync(id, cancellationToken);
        if (claim == null)
            return NotFound(new { message = $"Expense claim {id} not found." });

        return Ok(new
        {
            claimId = claim.Id,
            claimNumber = claim.ClaimNumber,
            policyStatus = claim.PolicyStatus,
            violations = claim.Violations,
            complianceChecks = claim.ComplianceChecks
        });
    }

    // 6. Claim Audit Trail (Section 13)
    [HttpGet("claims/{id:guid}/audit")]
    [Authorize]
    public async Task<IActionResult> GetClaimAuditTrail(Guid id, CancellationToken cancellationToken)
    {
        var claim = await _workflowService.GetClaimDetailsAsync(id, cancellationToken);
        if (claim == null)
            return NotFound(new { message = $"Expense claim {id} not found." });

        return Ok(claim.AuditLogs);
    }

    // 7. Approve Claim (Section 13 & 14)
    [HttpPost("claims/{id:guid}/approve")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> ApproveClaim(Guid id, [FromBody] ManagerActionDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var managerId = GetAuthenticatedUserId();
            var result = await _workflowService.ApproveClaimAsync(id, managerId, dto?.Comment ?? "", cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // 8. Reject Claim (Section 13 & 14)
    [HttpPost("claims/{id:guid}/reject")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> RejectClaim(Guid id, [FromBody] ManagerActionDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto?.Comment))
            return BadRequest(new { message = "A specific rejection reason is required." });

        try
        {
            var managerId = GetAuthenticatedUserId();
            var result = await _workflowService.RejectClaimAsync(id, managerId, dto.Comment, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // 9. Request Revision (Section 13 & 14)
    [HttpPost("claims/{id:guid}/request-revision")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> RequestRevision(Guid id, [FromBody] ManagerActionDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto?.Comment))
            return BadRequest(new { message = "Detailed revision feedback comment is required." });

        try
        {
            var managerId = GetAuthenticatedUserId();
            var result = await _workflowService.RequestRevisionAsync(id, managerId, dto.Comment, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // 10. Employee Submits Claim
    [HttpPost("claims")]
    [Authorize(Roles = "Employee,Manager,Admin")]
    public async Task<IActionResult> SubmitClaim([FromBody] CreateExpenseClaimDto dto, CancellationToken cancellationToken)
    {
        if (dto.TotalAmount <= 0)
            return BadRequest(new { message = "Total amount must be greater than zero." });

        if (string.IsNullOrWhiteSpace(dto.Category))
            return BadRequest(new { message = "Expense category is required." });

        var employeeId = GetAuthenticatedUserId();
        var claim = await _workflowService.SubmitClaimAsync(employeeId, dto, cancellationToken);
        return CreatedAtAction(nameof(GetClaimById), new { id = claim.Id }, claim);
    }

    // 11. Employee Resubmits Revised Claim
    [HttpPut("claims/{id:guid}/resubmit")]
    [Authorize(Roles = "Employee,Manager,Admin")]
    public async Task<IActionResult> ResubmitClaim(Guid id, [FromBody] ResubmitExpenseClaimDto dto, CancellationToken cancellationToken)
    {
        if (dto.TotalAmount <= 0)
            return BadRequest(new { message = "Total amount must be greater than zero." });

        try
        {
            var employeeId = GetAuthenticatedUserId();
            var claim = await _workflowService.ResubmitClaimAsync(id, employeeId, dto, cancellationToken);
            return Ok(claim);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // 12. Employee Views My Claims
    [HttpGet("my-claims")]
    [Authorize]
    public async Task<IActionResult> GetMyClaims(CancellationToken cancellationToken)
    {
        var employeeId = GetAuthenticatedUserId();
        var claims = await _workflowService.GetEmployeeClaimsAsync(employeeId, cancellationToken);
        return Ok(claims);
    }

    // 13. Finance Reimburse Claim
    [HttpPost("claims/{id:guid}/reimburse")]
    [Authorize(Roles = "Finance,Admin")]
    public async Task<IActionResult> ReimburseClaim(Guid id, [FromBody] ManagerActionDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var financeUserId = GetAuthenticatedUserId();
            var claim = await _workflowService.ReimburseClaimAsync(id, financeUserId, dto?.Comment ?? "Payment disbursed via corporate wire.", cancellationToken);
            return Ok(claim);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

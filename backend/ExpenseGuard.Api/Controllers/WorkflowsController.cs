using ExpenseGuard.Api.Auth;
using ExpenseGuard.Api.Data;
using ExpenseGuard.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseGuard.Api.Controllers;

[ApiController]
[Route("api/workflows")]
[Authorize]
public sealed class WorkflowsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = RoleNames.Finance + "," + RoleNames.Admin)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await db.WorkflowExecutions.AsNoTracking()
            .OrderByDescending(w => w.UpdatedAt)
            .Select(w => new
            {
                w.WorkflowExecutionId, w.SubjectType, w.ExpenseClaimId, w.PurchaseRequestId,
                w.Objective, w.Status, w.CorrelationId, w.CreatedAt, w.UpdatedAt
            }).ToListAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = RoleNames.Finance + "," + RoleNames.Admin)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var workflow = await db.WorkflowExecutions.AsNoTracking()
            .Where(w => w.WorkflowExecutionId == id)
            .Select(w => new
            {
                w.WorkflowExecutionId, w.SubjectType, w.ExpenseClaimId, w.PurchaseRequestId,
                w.Objective, w.Status, w.CorrelationId, w.CreatedAt, w.UpdatedAt,
                Steps = w.Steps.OrderBy(s => s.Sequence).Select(s => new
                {
                    s.WorkflowStepId, s.Sequence, s.Name, s.Type, s.Status, s.Error,
                    ToolExecutions = s.ToolExecutions.Select(t => new
                    {
                        t.ToolExecutionId, t.ToolName, t.Status, t.Error, t.CreatedAt
                    }),
                    ValidationResults = s.ValidationResults.Select(v => new
                    {
                        v.ValidationResultId, v.Validator, v.IsValid, v.CreatedAt
                    })
                })
            }).SingleOrDefaultAsync(ct);
        return workflow is null ? NotFound() : Ok(workflow);
    }

    [HttpGet("{id:guid}/audit")]
    [Authorize(Roles = RoleNames.Finance + "," + RoleNames.Admin)]
    public async Task<IActionResult> Audit(Guid id, CancellationToken ct)
    {
        var workflow = await db.WorkflowExecutions.AsNoTracking()
            .Where(w => w.WorkflowExecutionId == id)
            .Select(w => new { w.WorkflowExecutionId, w.CorrelationId })
            .SingleOrDefaultAsync(ct);
        if (workflow is null) return NotFound();

        var entityId = workflow.WorkflowExecutionId.ToString();
        return Ok(await db.AuditLogs.AsNoTracking()
            .Where(a => a.CorrelationId == workflow.CorrelationId ||
                        (a.EntityType == "WorkflowExecution" && a.EntityId == entityId))
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.AuditLogId, a.EmployeeId, a.Action, a.EntityType, a.EntityId,
                a.CorrelationId, a.CreatedAt
            }).ToListAsync(ct));
    }

    [HttpGet("claim/{claimId:int}")]
    public async Task<IActionResult> ForClaim(int claimId, CancellationToken ct)
    {
        var canAccess = User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Finance) ||
            await db.ExpenseClaims.AnyAsync(c => c.ExpenseClaimId == claimId &&
                c.EmployeeId == User.EmployeeId(), ct);
        if (!canAccess) return Forbid();

        var workflow = await db.WorkflowExecutions.AsNoTracking()
            .Where(w => w.ExpenseClaimId == claimId)
            .OrderByDescending(w => w.UpdatedAt)
            .Select(w => new
            {
                w.WorkflowExecutionId, w.SubjectType, w.ExpenseClaimId, w.PurchaseRequestId,
                w.Objective, w.Status, w.CorrelationId, w.CreatedAt, w.UpdatedAt,
                Steps = w.Steps.OrderBy(s => s.Sequence).Select(s => new
                {
                    s.WorkflowStepId, s.Sequence, s.Name, s.Type, s.Status, s.Error
                })
            }).FirstOrDefaultAsync(ct);
        return workflow is null ? NotFound() : Ok(workflow);
    }
}

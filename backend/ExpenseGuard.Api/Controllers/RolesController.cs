using System.Text.Json;
using ExpenseGuard.Api.Auth;
using ExpenseGuard.Api.Data;
using ExpenseGuard.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseGuard.Api.Controllers;

public record AssignRoleRequest(int RoleId);

[ApiController]
[Route("api/roles")]
[Authorize(Policy = "AdminOnly")]
public sealed class RolesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await db.Roles.AsNoTracking().OrderBy(r => r.RoleId)
            .Select(r => new
            {
                r.RoleId, r.RoleName, r.ApprovalLimit, r.CanApprove,
                r.CanProcessPayments, r.CanManageRoles, r.PermissionsJson
            }).ToListAsync(ct));

    [HttpPut("employees/{employeeId:int}")]
    public async Task<IActionResult> Assign(int employeeId, AssignRoleRequest request, CancellationToken ct)
    {
        var employee = await db.Employees.FindAsync([employeeId], ct);
        var role = await db.Roles.FindAsync([request.RoleId], ct);
        if (employee is null || role is null) return NotFound();
        var previousRoleId = employee.RoleId;
        employee.RoleId = role.RoleId;
        db.AuditLogs.Add(new AuditLog
        {
            EmployeeId = User.EmployeeId(),
            Action = "ROLE_ASSIGNED",
            EntityType = nameof(Employee),
            EntityId = employeeId.ToString(),
            CorrelationId = HttpContext.TraceIdentifier,
            DataJson = JsonSerializer.Serialize(new { previousRoleId, roleId = role.RoleId, role.RoleName })
        });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}

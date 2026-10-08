using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ExpenseGuard.Api.Data;
using ExpenseGuard.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ExpenseGuard.Api.Auth;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "ExpenseGuard";
    public string Audience { get; set; } = "ExpenseGuard.Clients";
    public string SigningKey { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
}

public record RegisterRequest(string Username, string Password, int DepartmentId);
public record LoginRequest(string Username, string Password);
public record AuthResponse(string Token, DateTime ExpiresAt, int EmployeeId, string Username, string Role);

public interface ITokenService
{
    AuthResponse Issue(Employee employee);
}

public sealed class TokenService(IOptions<JwtOptions> options) : ITokenService
{
    public AuthResponse Issue(Employee employee)
    {
        var settings = options.Value;
        if (Encoding.UTF8.GetByteCount(settings.SigningKey) < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be supplied by environment and be at least 32 bytes.");

        var expires = DateTime.UtcNow.AddMinutes(settings.ExpiryMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, employee.EmployeeId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, employee.EmployeeId.ToString()),
            new Claim(ClaimTypes.Name, employee.Username),
            new Claim(ClaimTypes.Role, employee.Role.RoleName),
            new Claim("department_id", employee.DepartmentId.ToString())
        };
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims,
            expires: expires, signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                SecurityAlgorithms.HmacSha256));
        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), expires,
            employee.EmployeeId, employee.Username, employee.Role.RoleName);
    }
}

public static class ClaimsPrincipalExtensions
{
    public static int EmployeeId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new UnauthorizedAccessException("Employee identity is missing.");

    public static bool CanAccessEmployee(this ClaimsPrincipal user, int employeeId) =>
        user.EmployeeId() == employeeId || user.IsInRole(RoleNames.Admin) || user.IsInRole(RoleNames.Finance);
}

public sealed class OwnsReimbursementRequirement : IAuthorizationRequirement;

public sealed class OwnsReimbursementHandler(AppDbContext db)
    : AuthorizationHandler<OwnsReimbursementRequirement, int>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, OwnsReimbursementRequirement requirement, int reimbursementId)
    {
        if (context.User.IsInRole(RoleNames.Admin) || context.User.IsInRole(RoleNames.Finance))
        {
            context.Succeed(requirement);
            return;
        }
        var employeeId = context.User.EmployeeId();
        if (await db.Reimbursements.AnyAsync(r =>
                r.ReimbursementId == reimbursementId && r.ExpenseClaim.EmployeeId == employeeId))
        {
            context.Succeed(requirement);
            return;
        }

        var role = context.User.FindFirstValue(ClaimTypes.Role);
        if (role is not null && await db.ApprovalProcesses.AnyAsync(p =>
                p.ReimbursementId == reimbursementId &&
                p.Status == ApprovalStatuses.Pending &&
                p.Steps.Any(s => s.Sequence == p.CurrentSequence && s.RequiredRole == role)))
        {
            context.Succeed(requirement);
            return;
        }

        if (await db.ApprovalProcesses.AnyAsync(p =>
                p.ReimbursementId == reimbursementId &&
                p.Steps.Any(s => s.DecidedByEmployeeId == employeeId)))
            context.Succeed(requirement);
    }
}

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AppDbContext db, ITokenService tokens) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var username = request.Username.Trim().ToLowerInvariant();
        if (username.Length is < 3 or > 100 || request.Password.Length < 12)
            return ValidationProblem("Username must be 3-100 characters and password at least 12 characters.");
        if (await db.Employees.AnyAsync(e => e.Username == username))
            return Conflict(new { error = "Username already exists." });
        if (!await db.Departments.AnyAsync(d => d.DepartmentId == request.DepartmentId))
            return BadRequest(new { error = "Department does not exist." });
        var role = await db.Roles.SingleAsync(r => r.RoleName == RoleNames.Employee);
        var employee = new Employee
        {
            Username = username,
            NormalizedUsername = username,
            FullName = username,
            Email = $"{username}@expenseguard.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            DepartmentId = request.DepartmentId,
            RoleId = role.RoleId,
            Role = role
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return Created("", tokens.Issue(employee));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var username = request.Username.Trim().ToLowerInvariant();
        var employee = await db.Employees.Include(e => e.Role)
            .SingleOrDefaultAsync(e => e.Username == username && e.IsActive && !e.IsLocked);
        if (employee is null || !BCrypt.Net.BCrypt.Verify(request.Password, employee.PasswordHash))
            return Unauthorized(new { error = "Invalid credentials." });
        return Ok(tokens.Issue(employee));
    }
}

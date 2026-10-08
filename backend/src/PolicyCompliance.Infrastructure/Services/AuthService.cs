using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PolicyCompliance.Core.DTOs;
using PolicyCompliance.Core.Services;
using PolicyCompliance.Infrastructure.Data;

namespace PolicyCompliance.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.ToLower(), cancellationToken);

        if (user == null)
            return null;

        string hashedInput = HashPassword(request.Password);
        if (user.PasswordHash != hashedInput)
            return null;

        string token = GenerateJwtToken(user);

        return new LoginResponseDto
        {
            Token = token,
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role.ToString(),
            DepartmentId = user.DepartmentId,
            DepartmentName = user.Department?.Name ?? "General"
        };
    }

    public async Task<UserDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null) return null;

        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role.ToString(),
            DepartmentId = user.DepartmentId,
            DepartmentName = user.Department?.Name ?? ""
        };
    }

    private string GenerateJwtToken(Core.Entities.User user)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? "CorporateSpendAndComplianceSuperSecretKey2026!#$";
        var issuer = _configuration["Jwt:Issuer"] ?? "PolicyComplianceApi";
        var audience = _configuration["Jwt:Audience"] ?? "PolicyComplianceClients";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("departmentId", user.DepartmentId.ToString()),
            new("fullName", user.FullName),
            new(ClaimTypes.Email, user.Email)
        };

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(7),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password + "PolicySalt2026!");
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}

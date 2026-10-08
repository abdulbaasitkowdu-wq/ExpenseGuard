using PolicyCompliance.Core.DTOs;

namespace PolicyCompliance.Core.Services;

public interface IAuthService
{
    Task<LoginResponseDto?> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<UserDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

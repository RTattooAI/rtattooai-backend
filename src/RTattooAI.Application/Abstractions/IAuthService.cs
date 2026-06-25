using RTattooAI.Application.Dtos.Auth;

namespace RTattooAI.Application.Abstractions;

public interface IAuthService
{
    Task<LoginResponseDto?> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
}

using RTattooAI.Application.Dtos.Auth;

namespace RTattooAI.Application.Abstractions;

public interface IAuthService
{
    Task<LoginResponseDto?> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Cambio de contraseña del usuario autenticado. Falso si la actual no coincide.</summary>
    Task<bool> ChangePasswordAsync(Guid usuarioId, ChangePasswordRequestDto request, CancellationToken cancellationToken = default);
}

using RTattooAI.Application.Dtos.Auth;

namespace RTattooAI.Application.Abstractions;

public interface IAuthService
{
    Task<LoginResponseDto?> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Cambio de contraseña del usuario autenticado. Falso si la actual no coincide.</summary>
    Task<bool> ChangePasswordAsync(Guid usuarioId, ChangePasswordRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Envía el enlace de recuperación si el correo pertenece a una cuenta activa.
    /// Nunca informa si el correo existe: siempre termina en silencio, para que la
    /// pantalla pública de "olvidé mi contraseña" no sirva para enumerar cuentas.
    /// </summary>
    Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Aplica la nueva contraseña si el token es válido y no ha caducado. Falso en cualquier otro caso.</summary>
    Task<bool> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default);
}

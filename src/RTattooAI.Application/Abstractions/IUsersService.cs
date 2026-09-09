using RTattooAI.Application.Dtos.Users;

namespace RTattooAI.Application.Abstractions;

public interface IUsersService
{
    Task<List<UserListItemDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<UserListItemDto> CreateAsync(CreateUserRequestDto request, CancellationToken cancellationToken = default);

    Task<UserListItemDto?> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default);

    /// <summary>Restablece la contraseña de una cuenta. Sustituye al "¿olvidó su contraseña?" inerte.</summary>
    Task<UserListItemDto?> ResetPasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken = default);
}

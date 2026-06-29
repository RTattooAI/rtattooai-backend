using RTattooAI.Application.Dtos.Users;

namespace RTattooAI.Application.Abstractions;

public interface IUsersService
{
    Task<List<UserListItemDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<UserListItemDto> CreateAsync(CreateUserRequestDto request, CancellationToken cancellationToken = default);

    Task<UserListItemDto?> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default);
}

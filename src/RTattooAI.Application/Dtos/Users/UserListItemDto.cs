namespace RTattooAI.Application.Dtos.Users;

public record UserListItemDto(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    bool Active,
    DateTime CreatedAt,
    string? Dependency = null,
    DateTime? LastAccessAt = null);

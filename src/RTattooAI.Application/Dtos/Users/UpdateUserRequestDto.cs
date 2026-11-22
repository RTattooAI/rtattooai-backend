namespace RTattooAI.Application.Dtos.Users;

/// <summary>Edición de una cuenta existente: no toca la contraseña ni el estado.</summary>
public record UpdateUserRequestDto(string FullName, string Role, string? Dependency = null);

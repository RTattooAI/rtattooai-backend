namespace RTattooAI.Application.Dtos.Users;

/// <summary>
/// Alta de usuario (RF-12). <c>Dependency</c> es la dependencia policial a la que
/// está adscrito: aparece en la auditoría y permite saber de qué unidad salió cada
/// consulta cuando el sistema se usa en más de una comisaría.
/// </summary>
public record CreateUserRequestDto(
    string FullName,
    string Email,
    string Password,
    string Role,
    string? Dependency = null);

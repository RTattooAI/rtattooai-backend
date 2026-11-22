using System.Security.Claims;
using RTattooAI.Application.Constants;

namespace RTattooAI.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id)
            ? id
            : throw new UnauthorizedAccessException("El token no contiene un identificador de usuario válido.");
    }

    public static string? GetEmail(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Email);

    public static string? GetRole(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Role);

    /// <summary>
    /// Supervisor y administrador pueden ver el trabajo de cualquier analista: es lo que
    /// hace atendible la bandeja de segunda revisión. Un analista solo ve lo suyo.
    /// </summary>
    public static bool CanSupervise(this ClaimsPrincipal user) =>
        user.IsInRole(Roles.Supervisor) || user.IsInRole(Roles.Admin);
}

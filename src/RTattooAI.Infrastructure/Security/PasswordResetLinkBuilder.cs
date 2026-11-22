using Microsoft.Extensions.Configuration;
using RTattooAI.Application.Abstractions;

namespace RTattooAI.Infrastructure.Security;

/// <summary>
/// Reutiliza Cors:AllowedOrigins (el origen del frontend desplegado, ya conocido por
/// CORS) en vez de pedir una URL nueva: si el navegador puede llamar desde ahí, ahí
/// mismo debe abrir el enlace que llega por correo.
/// </summary>
public class PasswordResetLinkBuilder : IPasswordResetLinkBuilder
{
    private readonly string _baseUrl;

    public PasswordResetLinkBuilder(IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        _baseUrl = (origins?.FirstOrDefault() ?? "http://localhost:4200").TrimEnd('/');
    }

    public string Build(string token) => $"{_baseUrl}/reset-password?token={Uri.EscapeDataString(token)}";
}

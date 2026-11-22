using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using RTattooAI.Application.Abstractions;

namespace RTattooAI.Infrastructure.Blob;

/// <summary>
/// Firma HMAC-SHA256 sobre "ruta|caducidad". La clave sale de Files:SigningKey y,
/// si no está configurada, de Jwt:Key (que siempre existe): así un despliegue
/// nuevo no queda con una clave por defecto conocida.
/// </summary>
public class FileUrlSigner : IFileUrlSigner
{
    private const string RoutePrefix = "/api/files/";

    private readonly byte[] _key;
    private readonly string _publicBaseUrl;

    public FileUrlSigner(IConfiguration configuration)
    {
        var key = configuration["Files:SigningKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            key = configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("Files:SigningKey (o Jwt:Key) no está configurado.");
        }

        _key = Encoding.UTF8.GetBytes(key);
        _publicBaseUrl = (configuration["Files:PublicBaseUrl"] ?? string.Empty).TrimEnd('/');
    }

    public string BuildSignedPath(string storagePath, TimeSpan validFor)
    {
        var normalized = Normalize(storagePath);
        var expiresAt = DateTimeOffset.UtcNow.Add(validFor).ToUnixTimeSeconds();
        var signature = Compute(normalized, expiresAt);

        return $"{_publicBaseUrl}{RoutePrefix}{normalized}?exp={expiresAt}&sig={signature}";
    }

    public bool Verify(string storagePath, long expiresAtUnixSeconds, string signature)
    {
        if (string.IsNullOrWhiteSpace(signature) || expiresAtUnixSeconds <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            return false;
        }

        var expected = Compute(Normalize(storagePath), expiresAtUnixSeconds);

        // Comparación en tiempo constante: evita distinguir firmas por el tiempo de respuesta.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(signature));
    }

    private static string Normalize(string storagePath) => storagePath.Replace('\\', '/').TrimStart('/');

    private string Compute(string normalizedPath, long expiresAt)
    {
        var payload = Encoding.UTF8.GetBytes($"{normalizedPath}|{expiresAt}");
        var hash = HMACSHA256.HashData(_key, payload);
        return Convert.ToHexStringLower(hash);
    }
}

using Microsoft.Extensions.Configuration;
using RTattooAI.Application.Abstractions;

namespace RTattooAI.Infrastructure.Blob;

/// <summary>
/// Almacenamiento en disco para el despliegue en infraestructura propia (sin Azure).
/// Las rutas lógicas ("consultas/{id}/original.jpg") se resuelven bajo un único
/// directorio raíz montado como volumen; la lectura se expone mediante URL firmada.
/// </summary>
public class LocalFileStorage : IBlobStorage
{
    private readonly string _rootPath;
    private readonly IFileUrlSigner _signer;

    public LocalFileStorage(IConfiguration configuration, IFileUrlSigner signer)
    {
        _rootPath = Path.GetFullPath(configuration["Storage:RootPath"] ?? "/data/files");
        _signer = signer;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task UploadAsync(string blobPath, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveWithinRoot(blobPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var file = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(file, cancellationToken);
    }

    public async Task<byte[]> DownloadAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveWithinRoot(blobPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"No existe el archivo '{blobPath}'.");
        }

        return await File.ReadAllBytesAsync(fullPath, cancellationToken);
    }

    public string GetReadUrl(string blobPath, TimeSpan validFor) => _signer.BuildSignedPath(blobPath, validFor);

    public bool Exists(string blobPath)
    {
        try
        {
            return File.Exists(ResolveWithinRoot(blobPath));
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public Stream OpenRead(string blobPath)
    {
        var fullPath = ResolveWithinRoot(blobPath);
        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    /// <summary>
    /// Resuelve la ruta lógica dentro de la raíz y rechaza cualquier intento de salir
    /// de ella (path traversal), aunque la firma HMAC ya impide construir rutas ajenas.
    /// </summary>
    private string ResolveWithinRoot(string blobPath)
    {
        var normalized = blobPath.Replace('\\', '/').TrimStart('/');
        var candidate = Path.GetFullPath(Path.Combine(_rootPath, normalized));

        if (!candidate.StartsWith(_rootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !string.Equals(candidate, _rootPath, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException($"Ruta de archivo fuera del almacenamiento: '{blobPath}'.");
        }

        return candidate;
    }
}

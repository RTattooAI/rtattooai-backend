using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RTattooAI.Application.Abstractions;

namespace RTattooAI.Api.Controllers;

/// <summary>
/// Entrega las imágenes almacenadas (consultas y catálogo). No usa el token JWT
/// porque el navegador no puede añadir headers a un &lt;img src&gt;: la autorización
/// es la firma HMAC con caducidad que genera <see cref="IFileUrlSigner"/>.
/// </summary>
[ApiController]
[Route("api/files")]
[AllowAnonymous]
public partial class FilesController : ControllerBase
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp"
    };

    private readonly IBlobStorage _blobStorage;
    private readonly IFileUrlSigner _signer;

    public FilesController(IBlobStorage blobStorage, IFileUrlSigner signer)
    {
        _blobStorage = blobStorage;
        _signer = signer;
    }

    [HttpGet("{**path}")]
    public async Task<IActionResult> Get(string path, [FromQuery] long exp, [FromQuery] string? sig, CancellationToken cancellationToken)
    {
        // Respuesta uniforme (404) ante ruta inválida, firma inválida o archivo ausente:
        // no se revela si un identificador existe.
        if (!SafePath().IsMatch(path) || path.Contains("..", StringComparison.Ordinal))
        {
            return NotFound();
        }

        var extension = Path.GetExtension(path);
        if (!ContentTypes.TryGetValue(extension, out var contentType))
        {
            return NotFound();
        }

        if (!_signer.Verify(path, exp, sig ?? string.Empty))
        {
            return NotFound();
        }

        byte[] content;
        try
        {
            content = await _blobStorage.DownloadAsync(path, cancellationToken);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "private, max-age=600";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentDisposition = "inline";

        return File(content, contentType);
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9/_\-.]{0,200}$")]
    private static partial Regex SafePath();
}

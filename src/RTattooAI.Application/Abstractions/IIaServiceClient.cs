using RTattooAI.Application.Dtos.Ia;

namespace RTattooAI.Application.Abstractions;

public interface IIaServiceClient
{
    Task<IdentifyResultDto> IdentifyAsync(Stream image, string fileName, string contentType, int topK = 5, CancellationToken cancellationToken = default);

    /// <param name="catalogId">
    /// Código con el que se guardan los vectores. Lo elige el backend, que es quien conoce
    /// los códigos del catálogo. Si ya tiene vectores en la galería se lanza
    /// <see cref="Exceptions.CatalogIdTakenException"/>.
    /// </param>
    Task<EnrollResultDto> EnrollAsync(Stream image, string fileName, string contentType, string band, string? catalogId = null, CancellationToken cancellationToken = default);

    /// <summary>Quita de la galería las vistas de un código. Falso si no tenía ninguna.</summary>
    Task<bool> RemoveFromGalleryAsync(string catalogId, CancellationToken cancellationToken = default);

    /// <summary>Códigos, vistas y huellas de la galería. Null si el servicio no responde.</summary>
    Task<GalleryStateDto?> GetGalleryAsync(CancellationToken cancellationToken = default);

    Task<bool> PingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Composición del modelo cargado (RF-16). Devuelve null si el servicio no responde:
    /// el mantenimiento de versiones debe poder consultarse aunque la inferencia esté caída.
    /// </summary>
    Task<IaInfoDto?> GetInfoAsync(CancellationToken cancellationToken = default);
}

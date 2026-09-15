namespace RTattooAI.Application.Dtos.Ia;

/// <summary>Contenido de la galería del servicio de inferencia, sin los vectores.</summary>
public record GalleryStateDto(
    IReadOnlyList<GalleryEntryDto> Entries,
    IReadOnlyList<IReadOnlyList<string>> SharedVectors);

/// <param name="ImageSha256">Huella de la imagen enrolada; null en vistas anteriores a que existiera.</param>
public record GalleryEntryDto(string CatalogId, int Views, string? ImageSha256);

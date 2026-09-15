namespace RTattooAI.Application.Dtos.Models;

/// <summary>
/// Coherencia entre el catálogo (base de datos) y la galería del servicio de inferencia.
/// Cada lista es una forma distinta de que el sistema devuelva un resultado equivocado
/// sin dar ningún error.
/// </summary>
/// <param name="MissingInGallery">Fichas sin vectores: el sistema nunca las reconocerá.</param>
/// <param name="OrphansInGallery">Vectores sin ficha: pueden ganar una comparación y no tienen a qué referirse.</param>
/// <param name="ImageMismatch">El código tiene vectores de una imagen distinta a la de su ficha.</param>
/// <param name="SharedVectors">Códigos que comparten un mismo vector: la misma imagen enrolada dos veces.</param>
/// <param name="Unverified">Códigos presentes en ambos lados cuyos vectores no tienen huella de imagen.</param>
public record GalleryConsistencyDto(
    bool ServiceAvailable,
    int CatalogEntries,
    int GalleryIdentities,
    IReadOnlyList<string> MissingInGallery,
    IReadOnlyList<string> OrphansInGallery,
    IReadOnlyList<string> ImageMismatch,
    IReadOnlyList<IReadOnlyList<string>> SharedVectors,
    int Unverified,
    bool Consistent);

public record GalleryRepairResultDto(
    IReadOnlyList<string> Reenrolled,
    IReadOnlyList<string> Removed,
    IReadOnlyList<GalleryRepairFailureDto> Failed,
    GalleryConsistencyDto After);

public record GalleryRepairFailureDto(string CatalogId, string Reason);

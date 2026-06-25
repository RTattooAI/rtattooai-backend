namespace RTattooAI.Domain.Entities;

public class Coincidencia
{
    public Guid Id { get; set; }
    public Guid DeteccionId { get; set; }

    /// <summary>
    /// Código del tatuaje en la galería del servicio IA (p. ej. "tat_00123").
    /// Sin FK a TatuajeCatalogo: la galería del lado IA puede contener entradas
    /// que aún no tengan fila en SQL (p. ej. galería base preexistente).
    /// </summary>
    public required string CatalogoCodigo { get; set; }
    public required string Banda { get; set; }
    public double Similitud { get; set; }
    public string? ReferenceImageUrl { get; set; }

    public Deteccion? Deteccion { get; set; }
}

using RTattooAI.Application.Dtos.Models;

namespace RTattooAI.Application.Abstractions;

public interface IGalleryConsistencyService
{
    Task<GalleryConsistencyDto> CheckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Vuelve a enrolar desde su imagen guardada las fichas sin vectores, con vectores de
    /// otra imagen o con vectores compartidos. Nunca quita vectores sin ficha: no tener
    /// ficha en el catálogo no significa que sobren (ver el comentario de la clase que
    /// implementa esta interfaz).
    /// </summary>
    Task<GalleryRepairResultDto> RepairAsync(CancellationToken cancellationToken = default);
}

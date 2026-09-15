using RTattooAI.Application.Dtos.Models;

namespace RTattooAI.Application.Abstractions;

public interface IGalleryConsistencyService
{
    Task<GalleryConsistencyDto> CheckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Vuelve a enrolar desde su imagen guardada las fichas sin vectores, con vectores de
    /// otra imagen o con vectores compartidos, y quita los vectores sin ficha.
    /// </summary>
    Task<GalleryRepairResultDto> RepairAsync(CancellationToken cancellationToken = default);
}

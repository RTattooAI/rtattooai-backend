using RTattooAI.Application.Dtos.Bands;

namespace RTattooAI.Application.Abstractions;

/// <summary>
/// Mantenimiento de bandas criminales (RF-10). La banda es la unidad de agrupación
/// del catálogo: cada tatuaje de referencia pertenece a una, y la coincidencia que
/// devuelve el sistema se expresa en términos de banda.
/// </summary>
public interface IBandsService
{
    Task<List<BandDto>> GetAllAsync(bool includeInactive = true, CancellationToken cancellationToken = default);

    Task<BandDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<BandDto> CreateAsync(SaveBandRequestDto request, Guid usuarioId, CancellationToken cancellationToken = default);

    Task<BandDto?> UpdateAsync(Guid id, SaveBandRequestDto request, CancellationToken cancellationToken = default);

    Task<BandDto?> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default);

    /// <summary>
    /// Borra una banda. Solo se permite si no tiene tatuajes asociados: borrarla con
    /// catálogo detrás dejaría huérfanas entradas que sustentan resultados ya emitidos.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

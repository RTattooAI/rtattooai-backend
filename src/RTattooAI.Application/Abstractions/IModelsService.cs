using RTattooAI.Application.Dtos.Models;

namespace RTattooAI.Application.Abstractions;

/// <summary>
/// Versionado de modelos (RF-16). Cada consulta graba la versión con la que se
/// resolvió; este servicio conserva las métricas de cada versión para que un
/// resultado antiguo se pueda leer con las cifras que le corresponden y no con las
/// del modelo que esté cargado hoy.
/// </summary>
public interface IModelsService
{
    Task<ModelInventoryDto> GetInventoryAsync(CancellationToken cancellationToken = default);

    /// <summary>Registra o corrige las métricas de una versión, identificada por su cadena.</summary>
    Task<ModelVersionDto> UpsertAsync(SaveModelVersionRequestDto request, CancellationToken cancellationToken = default);
}

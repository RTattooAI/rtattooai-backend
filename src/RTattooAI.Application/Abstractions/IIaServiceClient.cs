using RTattooAI.Application.Dtos.Ia;

namespace RTattooAI.Application.Abstractions;

public interface IIaServiceClient
{
    Task<IdentifyResultDto> IdentifyAsync(Stream image, string fileName, string contentType, int topK = 5, CancellationToken cancellationToken = default);

    Task<EnrollResultDto> EnrollAsync(Stream image, string fileName, string contentType, string band, CancellationToken cancellationToken = default);

    Task<bool> PingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Composición del modelo cargado (RF-16). Devuelve null si el servicio no responde:
    /// el mantenimiento de versiones debe poder consultarse aunque la inferencia esté caída.
    /// </summary>
    Task<IaInfoDto?> GetInfoAsync(CancellationToken cancellationToken = default);
}

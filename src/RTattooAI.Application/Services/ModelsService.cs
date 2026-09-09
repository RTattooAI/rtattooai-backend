using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Models;
using RTattooAI.Application.Exceptions;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Application.Services;

public class ModelsService : IModelsService
{
    private readonly IVersionModeloRepository _repository;
    private readonly IIaServiceClient _iaServiceClient;

    public ModelsService(IVersionModeloRepository repository, IIaServiceClient iaServiceClient)
    {
        _repository = repository;
        _iaServiceClient = iaServiceClient;
    }

    public async Task<ModelInventoryDto> GetInventoryAsync(CancellationToken cancellationToken = default)
    {
        var runtime = await _iaServiceClient.GetInfoAsync(cancellationToken);

        // Si el servicio reporta una version que no esta registrada, se registra en el
        // acto: de otro modo habria consultas atribuidas a una version de la que no
        // consta nada, que es justamente lo que RF-16 quiere evitar.
        if (runtime is not null)
        {
            await AsegurarRegistradaAsync(runtime.ModelVersion, runtime, cancellationToken);
        }

        var versiones = await _repository.GetAllAsync(cancellationToken);
        var conteos = await _repository.CountConsultasPorVersionAsync(cancellationToken);

        var activa = runtime?.ModelVersion;

        return new ModelInventoryDto(
            runtime is null
                ? null
                : new RuntimeModelDto(
                    runtime.ModelVersion, runtime.Detector, runtime.Embedder, runtime.Head, runtime.EmbeddingDim,
                    runtime.Threshold, runtime.Margin, runtime.DetConf, runtime.DetIou,
                    runtime.GallerySize, runtime.GalleryIdentities, runtime.GalleryBands),
            [.. versiones.Select(v => ToDto(v, conteos, activa))]);
    }

    public async Task<ModelVersionDto> UpsertAsync(SaveModelVersionRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            throw new InvalidRequestException("El identificador de la versión es obligatorio.");
        }

        var identificador = request.Identifier.Trim();
        var version = await _repository.GetByIdentificadorAsync(identificador, cancellationToken);

        if (version is null)
        {
            version = new VersionModelo { Id = Guid.NewGuid(), Identificador = identificador };
            await _repository.AddAsync(version, cancellationToken);
        }

        version.Detector = request.Detector ?? version.Detector;
        version.Embedder = request.Embedder ?? version.Embedder;
        version.Cabeza = request.Head ?? version.Cabeza;
        version.MetricaMap50 = request.Map50 ?? version.MetricaMap50;
        version.MetricaTop1 = request.Top1 ?? version.MetricaTop1;
        version.MetricaTop5 = request.Top5 ?? version.MetricaTop5;
        version.Umbral = request.Threshold ?? version.Umbral;
        version.Margen = request.Margin ?? version.Margen;
        version.Fuente = request.Source ?? version.Fuente;
        version.Notas = request.Notes ?? version.Notas;

        await _repository.SaveChangesAsync(cancellationToken);

        var conteos = await _repository.CountConsultasPorVersionAsync(cancellationToken);
        return ToDto(version, conteos, null);
    }

    private async Task AsegurarRegistradaAsync(string identificador, Dtos.Ia.IaInfoDto runtime, CancellationToken cancellationToken)
    {
        var existente = await _repository.GetByIdentificadorAsync(identificador, cancellationToken);
        if (existente is not null)
        {
            return;
        }

        await _repository.AddAsync(new VersionModelo
        {
            Id = Guid.NewGuid(),
            Identificador = identificador,
            Detector = runtime.Detector,
            Embedder = runtime.Embedder,
            Cabeza = runtime.Head,
            Umbral = runtime.Threshold,
            Margen = runtime.Margin,
            Fuente = "detectada automáticamente del servicio de inferencia",
            EsActiva = true
        }, cancellationToken);

        await _repository.SaveChangesAsync(cancellationToken);
    }

    private static ModelVersionDto ToDto(VersionModelo v, IReadOnlyDictionary<string, int> conteos, string? activa) => new(
        v.Id,
        v.Identificador,
        v.Detector,
        v.Embedder,
        v.Cabeza,
        v.MetricaMap50,
        v.MetricaTop1,
        v.MetricaTop5,
        v.Umbral,
        v.Margen,
        v.Fuente,
        v.Notas,
        activa is not null && string.Equals(activa, v.Identificador, StringComparison.Ordinal),
        conteos.TryGetValue(v.Identificador, out var total) ? total : 0,
        v.FechaRegistro);
}

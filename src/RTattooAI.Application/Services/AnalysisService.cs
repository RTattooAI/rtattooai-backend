using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Constants;
using RTattooAI.Application.Dtos.Analyses;
using RTattooAI.Application.Dtos.Catalog;
using RTattooAI.Application.Exceptions;
using RTattooAI.Application.Validation;
using RTattooAI.Domain.Entities;
using RTattooAI.Domain.Enums;

namespace RTattooAI.Application.Services;

public class AnalysisService : IAnalysisService
{
    private const string FuenteDeteccionAnalisis = "deteccion_analisis";
    private const int ThumbnailMaxDimension = 240;
    private static readonly TimeSpan ImageUrlValidity = TimeSpan.FromHours(1);

    private readonly IConsultaRepository _consultaRepository;
    private readonly ITatuajeCatalogoRepository _tatuajeCatalogoRepository;
    private readonly IBlobStorage _blobStorage;
    private readonly IIaServiceClient _iaServiceClient;
    private readonly IImageCropper _imageCropper;
    private readonly ILogger<AnalysisService> _logger;

    public AnalysisService(
        IConsultaRepository consultaRepository,
        ITatuajeCatalogoRepository tatuajeCatalogoRepository,
        IBlobStorage blobStorage,
        IIaServiceClient iaServiceClient,
        IImageCropper imageCropper,
        ILogger<AnalysisService> logger)
    {
        _consultaRepository = consultaRepository;
        _tatuajeCatalogoRepository = tatuajeCatalogoRepository;
        _blobStorage = blobStorage;
        _iaServiceClient = iaServiceClient;
        _imageCropper = imageCropper;
        _logger = logger;
    }

    public async Task<AnalysisDto> CreateAsync(
        Guid usuarioId,
        Stream imageStream,
        string fileName,
        string contentType,
        long contentLength,
        string? personName = null,
        string? documentId = null,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        ImageValidator.EnsureValid(contentType, contentLength);

        using var buffer = new MemoryStream();
        await imageStream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var hashBytes = await SHA256.HashDataAsync(buffer, cancellationToken);
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        buffer.Position = 0;

        var consultaId = Guid.NewGuid();
        var extension = string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase) ? "png" : "jpg";
        var blobPath = $"consultas/{consultaId}/original.{extension}";

        await _blobStorage.UploadAsync(blobPath, buffer, contentType, cancellationToken);

        string? thumbnailBlobPath = null;
        try
        {
            var thumbnailBytes = _imageCropper.ResizeToThumbnailPng(buffer.ToArray(), ThumbnailMaxDimension);
            thumbnailBlobPath = $"consultas/{consultaId}/thumbnail.png";
            using var thumbnailStream = new MemoryStream(thumbnailBytes);
            await _blobStorage.UploadAsync(thumbnailBlobPath, thumbnailStream, "image/png", cancellationToken);
        }
        catch (Exception ex)
        {
            // La miniatura es solo para listas (historial); si falla, el analisis
            // sigue su curso normal y la lista cae al placeholder con el id.
            _logger.LogWarning(ex, "No se pudo generar miniatura para la consulta {ConsultaId}", consultaId);
            thumbnailBlobPath = null;
        }

        var consulta = new Consulta
        {
            Id = consultaId,
            UsuarioId = usuarioId,
            Codigo = $"CON-{DateTime.UtcNow:yyyyMMddHHmmss}-{consultaId.ToString("N")[..6]}",
            ImagenOrigUrl = blobPath,
            ImagenOrigHash = hash,
            ImagenMiniaturaUrl = thumbnailBlobPath,
            Estado = AnalysisStatus.Processing,
            FechaConsulta = DateTime.UtcNow,
            PersonaNombre = NullIfBlank(personName),
            PersonaDocumento = NullIfBlank(documentId),
            Notas = NullIfBlank(notes)
        };

        await _consultaRepository.AddAsync(consulta, cancellationToken);
        await _consultaRepository.SaveChangesAsync(cancellationToken);

        if (consulta.PersonaNombre is not null || consulta.PersonaDocumento is not null)
        {
            _logger.LogInformation(
                "Usuario {UsuarioId} registro datos de persona en la consulta {ConsultaId}",
                usuarioId, consulta.Id);
        }

        try
        {
            buffer.Position = 0;
            var result = await _iaServiceClient.IdentifyAsync(buffer, fileName, contentType, cancellationToken: cancellationToken);

            consulta.ImageWidth = result.ImageWidth;
            consulta.ImageHeight = result.ImageHeight;
            consulta.ModelVersion = result.ModelVersion;
            consulta.TiempoProcMs = result.ProcessingMs;
            consulta.FechaFin = DateTime.UtcNow;
            consulta.Estado = result.Detections.Count == 0 ? AnalysisStatus.NoDetection : AnalysisStatus.Completed;

            foreach (var detection in result.Detections)
            {
                var deteccion = new Deteccion
                {
                    Id = Guid.NewGuid(),
                    ConsultaId = consulta.Id,
                    BboxX = detection.Bbox.X,
                    BboxY = detection.Bbox.Y,
                    BboxWidth = detection.Bbox.Width,
                    BboxHeight = detection.Bbox.Height,
                    Confianza = detection.Confidence,
                    Verdict = string.Equals(detection.Verdict, "match", StringComparison.OrdinalIgnoreCase) ? Verdict.Match : Verdict.NoMatch
                };

                foreach (var match in detection.Matches)
                {
                    deteccion.Coincidencias.Add(new Coincidencia
                    {
                        Id = Guid.NewGuid(),
                        DeteccionId = deteccion.Id,
                        CatalogoCodigo = match.CatalogId,
                        Banda = match.Band,
                        Similitud = match.Similarity,
                        ReferenceImageUrl = match.ReferenceImageUrl
                    });
                }

                consulta.Detecciones.Add(deteccion);
            }

            await _consultaRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al invocar el servicio IA para la consulta {ConsultaId}", consulta.Id);

            consulta.Estado = AnalysisStatus.Failed;
            consulta.FechaFin = DateTime.UtcNow;
            await _consultaRepository.SaveChangesAsync(cancellationToken);
        }

        return await BuildDtoAsync(consulta, cancellationToken);
    }

    public async Task<AnalysisDto?> GetByIdAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var consulta = await _consultaRepository.GetByIdAsync(id, usuarioId, cancellationToken);
        if (consulta is null)
        {
            return null;
        }

        if (consulta.PersonaNombre is not null || consulta.PersonaDocumento is not null)
        {
            _logger.LogInformation(
                "Usuario {UsuarioId} accedio a datos de persona de la consulta {ConsultaId}",
                usuarioId, consulta.Id);
        }

        return await BuildDtoAsync(consulta, cancellationToken);
    }

    public async Task<List<AnalysisSummaryDto>> GetHistoryAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var consultas = await _consultaRepository.GetAllByUsuarioIdAsync(usuarioId, cancellationToken);

        var conPersona = consultas.Count(c => c.PersonaNombre is not null || c.PersonaDocumento is not null);
        if (conPersona > 0)
        {
            _logger.LogInformation(
                "Usuario {UsuarioId} listo el historial: {ConPersona} de {Total} consultas con datos de persona",
                usuarioId, conPersona, consultas.Count);
        }

        return consultas.Select(consulta =>
        {
            var thumbnailUrl = consulta.ImagenMiniaturaUrl is null
                ? null
                : _blobStorage.GetReadUrl(consulta.ImagenMiniaturaUrl, ImageUrlValidity);

            var topMatch = consulta.Detecciones
                .SelectMany(d => d.Coincidencias)
                .OrderByDescending(c => c.Similitud)
                .FirstOrDefault();

            return new AnalysisSummaryDto(
                consulta.Id,
                thumbnailUrl,
                ToContractStatus(consulta.Estado),
                consulta.FechaConsulta,
                topMatch?.Banda,
                topMatch?.Similitud,
                consulta.PersonaNombre,
                consulta.PersonaDocumento);
        }).ToList();
    }

    public async Task<List<CatalogItemDto>?> EnrollDetectionsAsync(Guid usuarioId, Guid analysisId, IReadOnlyList<Guid> detectionIds, CancellationToken cancellationToken = default)
    {
        if (detectionIds.Count == 0)
        {
            throw new InvalidRequestException("Debe seleccionar al menos una detección para agregar al catálogo.");
        }

        var consulta = await _consultaRepository.GetByIdAsync(analysisId, usuarioId, cancellationToken);
        if (consulta is null)
        {
            return null;
        }

        var detecciones = consulta.Detecciones.Where(d => detectionIds.Contains(d.Id)).ToList();
        if (detecciones.Count == 0)
        {
            throw new InvalidRequestException("Las detecciones indicadas no pertenecen a este análisis.");
        }

        var originalBytes = await _blobStorage.DownloadAsync(consulta.ImagenOrigUrl, cancellationToken);

        var results = new List<CatalogItemDto>();
        foreach (var deteccion in detecciones)
        {
            var cropBytes = _imageCropper.CropToPng(originalBytes, deteccion.BboxX, deteccion.BboxY, deteccion.BboxWidth, deteccion.BboxHeight);
            var cropHash = Convert.ToHexString(SHA256.HashData(cropBytes)).ToLowerInvariant();

            using var enrollStream = new MemoryStream(cropBytes);
            var enrollResult = await _iaServiceClient.EnrollAsync(enrollStream, $"{deteccion.Id}.png", "image/png", string.Empty, cancellationToken);

            var tatuajeId = Guid.NewGuid();
            var blobPath = $"catalogo/{tatuajeId}/original.png";
            using var uploadStream = new MemoryStream(cropBytes);
            await _blobStorage.UploadAsync(blobPath, uploadStream, "image/png", cancellationToken);

            var tatuaje = new TatuajeCatalogo
            {
                Id = tatuajeId,
                Codigo = enrollResult.CatalogId,
                BandaId = null,
                ConsultaOrigenId = consulta.Id,
                ImagenUrl = blobPath,
                ImagenHash = cropHash,
                Fuente = FuenteDeteccionAnalisis,
                Validado = false,
                FechaRegistro = DateTime.UtcNow,
                RegistradoPorId = usuarioId
            };

            await _tatuajeCatalogoRepository.AddAsync(tatuaje, cancellationToken);

            results.Add(new CatalogItemDto(
                tatuaje.Codigo,
                null,
                _blobStorage.GetReadUrl(tatuaje.ImagenUrl, ImageUrlValidity),
                tatuaje.Fuente,
                tatuaje.RegistradoPorId,
                tatuaje.FechaRegistro,
                tatuaje.Validado,
                tatuaje.ConsultaOrigenId));
        }

        await _tatuajeCatalogoRepository.SaveChangesAsync(cancellationToken);

        return results;
    }

    public async Task<DetectionDto?> ValidateDetectionAsync(Guid usuarioId, Guid analysisId, Guid detectionId, string status, string? notes, CancellationToken cancellationToken = default)
    {
        var estado = ParseValidationStatus(status);

        var consulta = await _consultaRepository.GetByIdAsync(analysisId, usuarioId, cancellationToken);
        var deteccion = consulta?.Detecciones.FirstOrDefault(d => d.Id == detectionId);
        if (deteccion is null)
        {
            return null;
        }

        deteccion.EstadoValidacion = estado;
        deteccion.ObservacionesPerito = NullIfBlank(notes);
        deteccion.ValidadoPorId = usuarioId;
        deteccion.FechaValidacion = DateTime.UtcNow;

        await _consultaRepository.SaveChangesAsync(cancellationToken);

        var codigos = deteccion.Coincidencias.Select(c => c.CatalogoCodigo);
        var catalogImagePaths = await _tatuajeCatalogoRepository.GetImagePathsByCodigosAsync(codigos, cancellationToken);

        return MapDetectionDto(deteccion, catalogImagePaths);
    }

    private async Task<AnalysisDto> BuildDtoAsync(Consulta consulta, CancellationToken cancellationToken)
    {
        var codigos = consulta.Detecciones.SelectMany(d => d.Coincidencias).Select(c => c.CatalogoCodigo);
        var catalogImagePaths = await _tatuajeCatalogoRepository.GetImagePathsByCodigosAsync(codigos, cancellationToken);

        var imageUrl = _blobStorage.GetReadUrl(consulta.ImagenOrigUrl, ImageUrlValidity);
        var thumbnailUrl = consulta.ImagenMiniaturaUrl is null
            ? null
            : _blobStorage.GetReadUrl(consulta.ImagenMiniaturaUrl, ImageUrlValidity);

        var detections = consulta.Detecciones.Select(d => MapDetectionDto(d, catalogImagePaths)).ToList();

        return new AnalysisDto(
            consulta.Id,
            ToContractStatus(consulta.Estado),
            imageUrl,
            thumbnailUrl,
            consulta.ImageWidth,
            consulta.ImageHeight,
            consulta.FechaConsulta,
            consulta.ModelVersion,
            consulta.TiempoProcMs,
            detections,
            Disclaimers.AnalysisDisclaimer,
            consulta.PersonaNombre,
            consulta.PersonaDocumento,
            consulta.Notas);
    }

    private DetectionDto MapDetectionDto(Deteccion d, Dictionary<string, string> catalogImagePaths) => new(
        d.Id,
        new BboxDto(d.BboxX, d.BboxY, d.BboxWidth, d.BboxHeight),
        d.Confianza,
        d.Verdict == Verdict.Match ? "match" : "no_match",
        d.Coincidencias
            .OrderByDescending(c => c.Similitud)
            .Select(c => new MatchDto(
                c.CatalogoCodigo,
                c.Banda,
                c.Similitud,
                catalogImagePaths.TryGetValue(c.CatalogoCodigo, out var imagePath) ? _blobStorage.GetReadUrl(imagePath, ImageUrlValidity) : c.ReferenceImageUrl))
            .ToList(),
        ToContractValidationStatus(d.EstadoValidacion),
        d.ObservacionesPerito);

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ToContractStatus(AnalysisStatus status) => status switch
    {
        AnalysisStatus.Processing => "processing",
        AnalysisStatus.Completed => "completed",
        AnalysisStatus.NoDetection => "no_detection",
        AnalysisStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private static string ToContractValidationStatus(EstadoValidacion estado) => estado switch
    {
        EstadoValidacion.Pendiente => "pending",
        EstadoValidacion.Validado => "validated",
        EstadoValidacion.EnRevision => "review",
        EstadoValidacion.Descartado => "dismissed",
        _ => throw new ArgumentOutOfRangeException(nameof(estado))
    };

    private static EstadoValidacion ParseValidationStatus(string status) => status switch
    {
        "validated" => EstadoValidacion.Validado,
        "review" => EstadoValidacion.EnRevision,
        "dismissed" => EstadoValidacion.Descartado,
        _ => throw new InvalidRequestException($"Estado de validación inválido: '{status}'.")
    };
}

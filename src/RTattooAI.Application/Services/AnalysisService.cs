using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Constants;
using RTattooAI.Application.Dtos.Analyses;
using RTattooAI.Application.Dtos.Catalog;
using RTattooAI.Application.Dtos.Common;
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

    /// <summary>Tope de fotografías por análisis: contiene el tiempo total de proceso y el tamaño de la petición.</summary>
    public const int MaxImagesPerAnalysis = 10;

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
        IReadOnlyList<AnalysisImageInput> images,
        string? personName = null,
        string? documentId = null,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        if (images.Count == 0)
        {
            throw new InvalidImageException("Debe adjuntar al menos una imagen.");
        }

        if (images.Count > MaxImagesPerAnalysis)
        {
            throw new InvalidImageException($"Un análisis admite como máximo {MaxImagesPerAnalysis} imágenes.");
        }

        // Se validan y bufferizan todas antes de escribir nada: si una es inválida, el
        // análisis no llega a crearse a medias.
        var preparadas = new List<(AnalysisImageInput Input, byte[] Bytes, ValidatedImage Meta)>();
        foreach (var input in images)
        {
            ImageValidator.EnsureAllowedUpload(input.ContentType, input.Length);

            using var buffer = new MemoryStream();
            await input.Content.CopyToAsync(buffer, cancellationToken);
            var bytes = buffer.ToArray();

            // El tipo real sale de los bytes, no de lo que declare el cliente.
            preparadas.Add((input, bytes, ImageValidator.EnsureValidContent(bytes)));
        }

        var consultaId = Guid.NewGuid();
        var consulta = new Consulta
        {
            Id = consultaId,
            UsuarioId = usuarioId,
            Codigo = $"CON-{DateTime.UtcNow:yyyyMMddHHmmss}-{consultaId.ToString("N")[..6]}",
            ImagenOrigUrl = string.Empty,
            ImagenOrigHash = string.Empty,
            Estado = AnalysisStatus.Processing,
            FechaConsulta = DateTime.UtcNow,
            PersonaNombre = NullIfBlank(personName),
            PersonaDocumento = NullIfBlank(documentId),
            Notas = NullIfBlank(notes)
        };

        for (var orden = 0; orden < preparadas.Count; orden++)
        {
            var (input, bytes, meta) = preparadas[orden];
            var imagenId = Guid.NewGuid();
            var blobPath = $"consultas/{consultaId}/{imagenId}/original.{meta.Extension}";

            using (var upload = new MemoryStream(bytes))
            {
                await _blobStorage.UploadAsync(blobPath, upload, meta.ContentType, cancellationToken);
            }

            consulta.Imagenes.Add(new ConsultaImagen
            {
                Id = imagenId,
                ConsultaId = consultaId,
                Orden = orden,
                ImagenUrl = blobPath,
                ImagenHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                ImagenMiniaturaUrl = await TryCreateThumbnailAsync(consultaId, imagenId, bytes, cancellationToken),
                Estado = AnalysisStatus.Processing,
                Origen = NormalizeOrigen(input.Source)
            });
        }

        AplicarPortada(consulta);

        await _consultaRepository.AddAsync(consulta, cancellationToken);
        await _consultaRepository.SaveChangesAsync(cancellationToken);

        if (consulta.PersonaNombre is not null || consulta.PersonaDocumento is not null)
        {
            _logger.LogInformation(
                "Usuario {UsuarioId} registro datos de persona en la consulta {ConsultaId}",
                usuarioId, consulta.Id);
        }

        var porOrden = consulta.Imagenes.OrderBy(i => i.Orden).ToList();
        for (var orden = 0; orden < porOrden.Count; orden++)
        {
            using var stream = new MemoryStream(preparadas[orden].Bytes);
            await RunIdentificationAsync(consulta, porOrden[orden], stream, preparadas[orden].Input.FileName,
                preparadas[orden].Meta.ContentType, cancellationToken);
        }

        ActualizarEstadoAgregado(consulta);
        AplicarPortada(consulta);
        await _consultaRepository.SaveChangesAsync(cancellationToken);

        return await BuildDtoAsync(consulta, cancellationToken);
    }

    public async Task<AnalysisPreviewDto> PreviewAsync(AnalysisImageInput image, CancellationToken cancellationToken = default)
    {
        ImageValidator.EnsureAllowedUpload(image.ContentType, image.Length);

        using var buffer = new MemoryStream();
        await image.Content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var meta = ImageValidator.EnsureValidContent(bytes);

        using var stream = new MemoryStream(bytes);
        var result = await _iaServiceClient.IdentifyAsync(stream, image.FileName, meta.ContentType, cancellationToken: cancellationToken);

        var detections = result.Detections
            .Select(d => new PreviewDetectionDto(
                new BboxDto(d.Bbox.X, d.Bbox.Y, d.Bbox.Width, d.Bbox.Height),
                d.Confidence,
                NormalizeVerdict(d.Verdict),
                d.Matches
                    .OrderByDescending(m => m.Similarity)
                    .Select(m => new MatchDto(m.CatalogId, m.Band, m.Similarity, m.ReferenceImageUrl))
                    .ToList()))
            .ToList();

        return new AnalysisPreviewDto(
            result.ImageWidth,
            result.ImageHeight,
            result.ModelVersion,
            result.ProcessingMs,
            detections,
            Disclaimers.LivePreviewDisclaimer);
    }

    public async Task<AnalysisDto?> RetryAsync(Guid usuarioId, Guid analysisId, bool canViewAll = false, CancellationToken cancellationToken = default)
    {
        var consulta = await _consultaRepository.GetByIdAsync(analysisId, canViewAll ? null : usuarioId, cancellationToken);
        if (consulta is null)
        {
            return null;
        }

        var fallidas = consulta.Imagenes.Where(i => i.Estado == AnalysisStatus.Failed).OrderBy(i => i.Orden).ToList();
        if (fallidas.Count == 0)
        {
            throw new InvalidRequestException("Solo se puede reintentar un análisis que haya fallado.");
        }

        foreach (var imagen in fallidas)
        {
            byte[] originalBytes;
            try
            {
                originalBytes = await _blobStorage.DownloadAsync(imagen.ImagenUrl, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo recuperar la imagen {ImagenId} de la consulta {ConsultaId} para reintentar",
                    imagen.Id, consulta.Id);
                throw new InvalidRequestException("No se pudo recuperar la imagen original del análisis.");
            }

            // Un reintento parte de cero para esa imagen: las detecciones del intento
            // anterior no deben mezclarse con las nuevas.
            foreach (var deteccion in imagen.Detecciones.ToList())
            {
                consulta.Detecciones.Remove(deteccion);
            }
            imagen.Detecciones.Clear();
            imagen.Estado = AnalysisStatus.Processing;

            var contentType = imagen.ImagenUrl.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png"
                : imagen.ImagenUrl.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ? "image/webp"
                : "image/jpeg";

            using var stream = new MemoryStream(originalBytes);
            await RunIdentificationAsync(consulta, imagen, stream, Path.GetFileName(imagen.ImagenUrl), contentType, cancellationToken);
        }

        ActualizarEstadoAgregado(consulta);
        await _consultaRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Usuario {UsuarioId} reintento {Cantidad} imagen(es) de la consulta {ConsultaId}",
            usuarioId, fallidas.Count, consulta.Id);

        return await BuildDtoAsync(consulta, cancellationToken);
    }

    public async Task<AnalysisDto?> GetByIdAsync(Guid id, Guid usuarioId, bool canViewAll = false, CancellationToken cancellationToken = default)
    {
        var consulta = await _consultaRepository.GetByIdAsync(id, canViewAll ? null : usuarioId, cancellationToken);
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

    public async Task<PagedResultDto<AnalysisSummaryDto>> GetHistoryAsync(
        Guid usuarioId,
        bool includeAllUsers = false,
        AnalysisHistoryQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        query ??= new AnalysisHistoryQuery();
        var estado = ParseStatus(query.Status);

        var (consultas, total) = await _consultaRepository.GetPagedAsync(
            includeAllUsers ? null : usuarioId, query, estado, cancellationToken);

        var conPersona = consultas.Count(c => c.PersonaNombre is not null || c.PersonaDocumento is not null);
        if (conPersona > 0)
        {
            _logger.LogInformation(
                "Usuario {UsuarioId} listo el historial: {ConPersona} de {Total} consultas con datos de persona",
                usuarioId, conPersona, consultas.Count);
        }

        return new PagedResultDto<AnalysisSummaryDto>(
            [.. consultas.Select(ToSummaryDto)], total, query.NormalizedPage, query.NormalizedPageSize);
    }

    /// <summary>
    /// Traduce el estado del contrato al del dominio. Un valor desconocido se ignora en
    /// vez de fallar: un filtro mal escrito no debe impedir ver el historial.
    /// </summary>
    private static AnalysisStatus? ParseStatus(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "completed" => AnalysisStatus.Completed,
        "processing" => AnalysisStatus.Processing,
        "no_detection" => AnalysisStatus.NoDetection,
        "failed" => AnalysisStatus.Failed,
        _ => null
    };

    public async Task<List<ReviewItemDto>> GetPendingReviewsAsync(CancellationToken cancellationToken = default)
    {
        var detecciones = await _consultaRepository.GetByEstadoValidacionAsync(EstadoValidacion.EnRevision, cancellationToken);

        return detecciones.Select(d =>
        {
            var topMatch = d.Coincidencias.OrderByDescending(c => c.Similitud).FirstOrDefault();
            var consulta = d.Consulta;
            var miniatura = d.ConsultaImagen?.ImagenMiniaturaUrl ?? consulta?.ImagenMiniaturaUrl;

            return new ReviewItemDto(
                d.ConsultaId,
                d.Id,
                miniatura is null ? null : _blobStorage.GetReadUrl(miniatura, ImageUrlValidity),
                d.Confianza,
                ToContractVerdict(d.Verdict),
                BandaDeResumen(topMatch),
                topMatch?.Similitud,
                d.ObservacionesPerito,
                d.ValidadoPor?.NombreCompleto ?? consulta?.Usuario?.NombreCompleto ?? "—",
                d.FechaValidacion,
                consulta?.PersonaNombre,
                consulta?.PersonaDocumento);
        }).ToList();
    }

    public async Task<string> ExportCsvAsync(Guid usuarioId, bool includeAllUsers, AnalysisHistoryQuery? query = null, CancellationToken cancellationToken = default)
    {
        query ??= new AnalysisHistoryQuery();

        var consultas = await _consultaRepository.GetFilteredAsync(
            includeAllUsers ? null : usuarioId, query, ParseStatus(query.Status), cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("analisis_id,codigo,fecha_utc,analista,estado,imagenes,version_modelo,persona,documento," +
                       "imagen_orden,imagen_origen,imagen_estado,tiempo_ms,deteccion_id,confianza,veredicto," +
                       "banda_top1,similitud_top1,estado_validacion,observaciones,fecha_validacion_utc");

        foreach (var consulta in consultas)
        {
            var analista = consulta.Usuario?.NombreCompleto ?? consulta.UsuarioId.ToString();
            var totalImagenes = consulta.Imagenes.Count.ToString(CultureInfo.InvariantCulture);

            string[] Cabecera() =>
            [
                Csv(consulta.Id.ToString()), Csv(consulta.Codigo), Csv(Iso(consulta.FechaConsulta)), Csv(analista),
                Csv(ToContractStatus(consulta.Estado)), Csv(totalImagenes), Csv(consulta.ModelVersion),
                Csv(consulta.PersonaNombre), Csv(consulta.PersonaDocumento)
            ];

            if (consulta.Imagenes.Count == 0)
            {
                csv.AppendLine(string.Join(',', [.. Cabecera(), "", "", "", "", "", "", "", "", "", "", "", ""]));
                continue;
            }

            foreach (var imagen in consulta.Imagenes.OrderBy(i => i.Orden))
            {
                string[] DatosImagen() =>
                [
                    Csv(imagen.Orden.ToString(CultureInfo.InvariantCulture)), Csv(imagen.Origen),
                    Csv(ToContractStatus(imagen.Estado)), Csv(imagen.TiempoProcMs?.ToString(CultureInfo.InvariantCulture))
                ];

                if (imagen.Detecciones.Count == 0)
                {
                    csv.AppendLine(string.Join(',', [.. Cabecera(), .. DatosImagen(), "", "", "", "", "", "", "", ""]));
                    continue;
                }

                foreach (var deteccion in imagen.Detecciones)
                {
                    var top = deteccion.Coincidencias.OrderByDescending(c => c.Similitud).FirstOrDefault();
                    csv.AppendLine(string.Join(',',
                    [
                        .. Cabecera(),
                        .. DatosImagen(),
                        Csv(deteccion.Id.ToString()),
                        Csv(deteccion.Confianza.ToString("F4", CultureInfo.InvariantCulture)),
                        Csv(ToContractVerdict(deteccion.Verdict)),
                        Csv(top?.Banda),
                        Csv(top?.Similitud.ToString("F4", CultureInfo.InvariantCulture)),
                        Csv(ToContractValidationStatus(deteccion.EstadoValidacion)),
                        Csv(deteccion.ObservacionesPerito),
                        Csv(deteccion.FechaValidacion is null ? null : Iso(deteccion.FechaValidacion.Value))
                    ]));
                }
            }
        }

        return csv.ToString();
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

        // Cada detección se recorta de SU propia imagen: con varias fotografías por
        // análisis, usar siempre la portada produciría recortes de la imagen equivocada.
        var imagenesPorId = consulta.Imagenes.ToDictionary(i => i.Id);
        var descargas = new Dictionary<Guid, byte[]>();

        var results = new List<CatalogItemDto>();
        foreach (var deteccion in detecciones)
        {
            if (!imagenesPorId.TryGetValue(deteccion.ConsultaImagenId, out var imagen))
            {
                throw new InvalidRequestException("La detección no tiene una imagen asociada válida.");
            }

            if (!descargas.TryGetValue(imagen.Id, out var originalBytes))
            {
                originalBytes = await _blobStorage.DownloadAsync(imagen.ImagenUrl, cancellationToken);
                descargas[imagen.Id] = originalBytes;
            }

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

    public async Task<DetectionDto?> ValidateDetectionAsync(Guid usuarioId, Guid analysisId, Guid detectionId, string status, string? notes, bool canViewAll = false, CancellationToken cancellationToken = default)
    {
        var estado = ParseValidationStatus(status);

        var consulta = await _consultaRepository.GetByIdAsync(analysisId, canViewAll ? null : usuarioId, cancellationToken);
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

    /// <summary>
    /// Invoca al servicio de IA para una imagen y persiste su resultado. Compartido por el
    /// análisis inicial y el reintento para que ambos caminos no puedan divergir.
    /// </summary>
    private async Task RunIdentificationAsync(Consulta consulta, ConsultaImagen imagen, Stream imageStream, string fileName, string contentType, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _iaServiceClient.IdentifyAsync(imageStream, fileName, contentType, cancellationToken: cancellationToken);

            imagen.Width = result.ImageWidth;
            imagen.Height = result.ImageHeight;
            imagen.ModelVersion = result.ModelVersion;
            imagen.TiempoProcMs = result.ProcessingMs;
            imagen.Estado = result.Detections.Count == 0 ? AnalysisStatus.NoDetection : AnalysisStatus.Completed;

            foreach (var detection in result.Detections)
            {
                var deteccion = new Deteccion
                {
                    Id = Guid.NewGuid(),
                    ConsultaId = consulta.Id,
                    ConsultaImagenId = imagen.Id,
                    BboxX = detection.Bbox.X,
                    BboxY = detection.Bbox.Y,
                    BboxWidth = detection.Bbox.Width,
                    BboxHeight = detection.Bbox.Height,
                    Confianza = detection.Confidence,
                    Verdict = ParseVerdict(detection.Verdict)
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

                imagen.Detecciones.Add(deteccion);
                consulta.Detecciones.Add(deteccion);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al invocar el servicio IA para la imagen {ImagenId} de la consulta {ConsultaId}",
                imagen.Id, consulta.Id);

            imagen.Estado = AnalysisStatus.Failed;
        }
    }

    /// <summary>
    /// Estado del análisis a partir del de sus imágenes: basta una imagen con detecciones
    /// para que el caso sea útil, y solo si todas fallan el análisis se considera fallido.
    /// </summary>
    private static void ActualizarEstadoAgregado(Consulta consulta)
    {
        var estados = consulta.Imagenes.Select(i => i.Estado).ToList();

        consulta.Estado = estados switch
        {
            _ when estados.Contains(AnalysisStatus.Completed) => AnalysisStatus.Completed,
            _ when estados.Contains(AnalysisStatus.NoDetection) => AnalysisStatus.NoDetection,
            _ when estados.Count > 0 && estados.All(e => e == AnalysisStatus.Failed) => AnalysisStatus.Failed,
            _ => AnalysisStatus.Processing
        };

        consulta.FechaFin = DateTime.UtcNow;
        consulta.TiempoProcMs = consulta.Imagenes.Sum(i => i.TiempoProcMs ?? 0) is var total && total > 0 ? total : null;
        consulta.ModelVersion = consulta.Imagenes.Select(i => i.ModelVersion).FirstOrDefault(v => v is not null);
    }

    /// <summary>Copia los datos de la primera imagen a los campos de portada de la consulta.</summary>
    private static void AplicarPortada(Consulta consulta)
    {
        var portada = consulta.Imagenes.OrderBy(i => i.Orden).FirstOrDefault();
        if (portada is null)
        {
            return;
        }

        consulta.ImagenOrigUrl = portada.ImagenUrl;
        consulta.ImagenOrigHash = portada.ImagenHash;
        consulta.ImagenMiniaturaUrl = portada.ImagenMiniaturaUrl;
        consulta.ImageWidth = portada.Width;
        consulta.ImageHeight = portada.Height;
    }

    private async Task<string?> TryCreateThumbnailAsync(Guid consultaId, Guid imagenId, byte[] imageBytes, CancellationToken cancellationToken)
    {
        try
        {
            var thumbnailBytes = _imageCropper.ResizeToThumbnailPng(imageBytes, ThumbnailMaxDimension);
            var thumbnailBlobPath = $"consultas/{consultaId}/{imagenId}/thumbnail.png";
            using var thumbnailStream = new MemoryStream(thumbnailBytes);
            await _blobStorage.UploadAsync(thumbnailBlobPath, thumbnailStream, "image/png", cancellationToken);
            return thumbnailBlobPath;
        }
        catch (Exception ex)
        {
            // La miniatura es solo para listas (historial); si falla, el analisis
            // sigue su curso normal y la lista cae al placeholder con el id.
            _logger.LogWarning(ex, "No se pudo generar miniatura para la imagen {ImagenId}", imagenId);
            return null;
        }
    }

    /// <summary>
    /// Banda del mejor candidato para el listado. Una entrada sin banda devolvía una
    /// cadena vacía, que en el historial se veía igual que "sin coincidencia".
    /// </summary>
    private static string? BandaDeResumen(Coincidencia? top) =>
        top is null ? null : (string.IsNullOrWhiteSpace(top.Banda) ? SinClasificar : top.Banda);

    /// <summary>Etiqueta con la que se muestra un reencuentro sin banda asignada.</summary>
    public const string SinClasificar = "Sin clasificar";

    private AnalysisSummaryDto ToSummaryDto(Consulta consulta)
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
            BandaDeResumen(topMatch),
            topMatch?.Similitud,
            consulta.PersonaNombre,
            consulta.PersonaDocumento,
            Math.Max(consulta.Imagenes.Count, 1));
    }

    private async Task<AnalysisDto> BuildDtoAsync(Consulta consulta, CancellationToken cancellationToken)
    {
        var codigos = consulta.Detecciones.SelectMany(d => d.Coincidencias).Select(c => c.CatalogoCodigo);
        var catalogImagePaths = await _tatuajeCatalogoRepository.GetImagePathsByCodigosAsync(codigos, cancellationToken);

        var imagenes = consulta.Imagenes
            .OrderBy(i => i.Orden)
            .Select(i => new AnalysisImageDto(
                i.Id,
                i.Orden,
                _blobStorage.GetReadUrl(i.ImagenUrl, ImageUrlValidity),
                i.ImagenMiniaturaUrl is null ? null : _blobStorage.GetReadUrl(i.ImagenMiniaturaUrl, ImageUrlValidity),
                i.Width,
                i.Height,
                ToContractStatus(i.Estado),
                i.ModelVersion,
                i.TiempoProcMs,
                i.Origen))
            .ToList();

        var portada = imagenes.FirstOrDefault();

        var detections = consulta.Detecciones
            .OrderBy(d => consulta.Imagenes.FirstOrDefault(i => i.Id == d.ConsultaImagenId)?.Orden ?? 0)
            .Select(d => MapDetectionDto(d, catalogImagePaths))
            .ToList();

        return new AnalysisDto(
            consulta.Id,
            ToContractStatus(consulta.Estado),
            portada?.ImageUrl ?? string.Empty,
            portada?.ThumbnailUrl,
            portada?.Width,
            portada?.Height,
            consulta.FechaConsulta,
            consulta.ModelVersion,
            consulta.TiempoProcMs,
            imagenes,
            detections,
            Disclaimers.AnalysisDisclaimer,
            consulta.PersonaNombre,
            consulta.PersonaDocumento,
            consulta.Notas);
    }

    private DetectionDto MapDetectionDto(Deteccion d, Dictionary<string, string> catalogImagePaths) => new(
        d.Id,
        d.ConsultaImagenId,
        new BboxDto(d.BboxX, d.BboxY, d.BboxWidth, d.BboxHeight),
        d.Confianza,
        ToContractVerdict(d.Verdict),
        d.Coincidencias
            .OrderByDescending(c => c.Similitud)
            .Select(c => new MatchDto(
                c.CatalogoCodigo,
                c.Banda,
                c.Similitud,
                catalogImagePaths.TryGetValue(c.CatalogoCodigo, out var imagePath) ? _blobStorage.GetReadUrl(imagePath, ImageUrlValidity) : c.ReferenceImageUrl))
            .ToList(),
        ToContractValidationStatus(d.EstadoValidacion),
        d.ObservacionesPerito,
        ResolveMatchType(d));

    /// <summary>
    /// Una coincidencia con una entrada sin banda no es "pertenece a los Pulpos":
    /// es "este tatuaje ya se registró en otro caso y nadie lo ha clasificado".
    /// Presentar ambas cosas igual llevaría al perito a atribuir una pertenencia
    /// que el sistema nunca afirmó.
    /// </summary>
    private static string ResolveMatchType(Deteccion d)
    {
        if (d.Verdict == Verdict.Review)
        {
            return MatchTypes.Review;
        }

        if (d.Verdict != Verdict.Match)
        {
            return MatchTypes.None;
        }

        var top = d.Coincidencias.OrderByDescending(c => c.Similitud).FirstOrDefault();
        return string.IsNullOrWhiteSpace(top?.Banda) ? MatchTypes.Unclassified : MatchTypes.Band;
    }

    private static string NormalizeOrigen(string? source) =>
        string.Equals(source, OrigenImagen.Camara, StringComparison.OrdinalIgnoreCase)
            ? OrigenImagen.Camara
            : OrigenImagen.Archivo;

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Iso(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture);

    /// <summary>Escapa un campo CSV; también neutraliza fórmulas para que Excel no las ejecute.</summary>
    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        var sanitized = value.Replace("\r", " ").Replace("\n", " ");
        if (sanitized.Length > 0 && sanitized[0] is '=' or '+' or '-' or '@')
        {
            sanitized = "'" + sanitized;
        }

        return $"\"{sanitized.Replace("\"", "\"\"")}\"";
    }

    private static string ToContractVerdict(Verdict verdict) => verdict switch
    {
        Verdict.Match => "match",
        Verdict.Review => "review",
        _ => "no_match"
    };

    /// <summary>
    /// Un veredicto desconocido se trata como "sin coincidencia". Si el servicio de
    /// inferencia introduce un valor nuevo, el sistema debe callar antes que afirmar
    /// algo que no entiende.
    /// </summary>
    private static Verdict ParseVerdict(string? verdict) => verdict?.Trim().ToLowerInvariant() switch
    {
        "match" => Verdict.Match,
        "review" => Verdict.Review,
        _ => Verdict.NoMatch
    };

    private static string NormalizeVerdict(string? verdict) => ToContractVerdict(ParseVerdict(verdict));

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

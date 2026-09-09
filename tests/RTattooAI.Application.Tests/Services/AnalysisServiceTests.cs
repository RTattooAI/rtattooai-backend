using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Analyses;
using RTattooAI.Application.Dtos.Ia;
using RTattooAI.Application.Exceptions;
using RTattooAI.Application.Services;
using RTattooAI.Application.Tests.TestData;
using RTattooAI.Domain.Entities;
using RTattooAI.Domain.Enums;
using Xunit;

namespace RTattooAI.Application.Tests.Services;

public class AnalysisServiceTests
{
    private readonly Mock<IConsultaRepository> _consultaRepository = new();
    private readonly Mock<ITatuajeCatalogoRepository> _tatuajeCatalogoRepository = new();
    private readonly Mock<IBlobStorage> _blobStorage = new();
    private readonly Mock<IIaServiceClient> _iaServiceClient = new();
    private readonly Mock<IImageCropper> _imageCropper = new();
    private readonly AnalysisService _sut;

    public AnalysisServiceTests()
    {
        _blobStorage.Setup(b => b.GetReadUrl(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns<string, TimeSpan>((path, _) => $"https://blob.test/{path}");
        _tatuajeCatalogoRepository.Setup(r => r.GetImagePathsByCodigosAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _sut = new AnalysisService(_consultaRepository.Object, _tatuajeCatalogoRepository.Object, _blobStorage.Object, _iaServiceClient.Object, _imageCropper.Object, NullLogger<AnalysisService>.Instance);
    }

    private static MemoryStream CreateImageStream() => TestImages.JpegStream();

    /// <summary>Una sola imagen, que es el caso mas frecuente en estas pruebas.</summary>
    private static IReadOnlyList<AnalysisImageInput> OneImage(
        string fileName = "file.jpg", string contentType = "image/jpeg", long length = 100, Stream? content = null) =>
        [new AnalysisImageInput(content ?? CreateImageStream(), fileName, contentType, length, "archivo")];

    [Fact]
    public async Task CreateAsync_ThrowsInvalidImageException_WhenContentTypeIsNotAllowed()
    {
        await Assert.ThrowsAsync<InvalidImageException>(() =>
            _sut.CreateAsync(Guid.NewGuid(), OneImage("file.txt", "text/plain", 100)));

        _consultaRepository.Verify(r => r.AddAsync(It.IsAny<Consulta>(), It.IsAny<CancellationToken>()), Times.Never);
        _blobStorage.Verify(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ThrowsInvalidImageException_WhenFileExceedsMaxSize()
    {
        await Assert.ThrowsAsync<InvalidImageException>(() =>
            _sut.CreateAsync(Guid.NewGuid(), OneImage("file.jpg", "image/jpeg", 11 * 1024 * 1024)));

        _consultaRepository.Verify(r => r.AddAsync(It.IsAny<Consulta>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_MarksCompleted_AndPersistsDetectionsAndMatches_WhenIaReturnsDetections()
    {
        Consulta? captured = null;
        _consultaRepository.Setup(r => r.AddAsync(It.IsAny<Consulta>(), It.IsAny<CancellationToken>()))
            .Callback<Consulta, CancellationToken>((c, _) => captured = c)
            .Returns(Task.CompletedTask);

        var iaResult = new IdentifyResultDto(1080, 1920, "yolov8m + dinov2-vits14 + head-v1", 540,
        [
            new IdentifyDetectionDto(new IdentifyBboxDto(120, 340, 200, 180), 0.91, "match",
            [
                new IdentifyMatchDto("tat_00123", "Los Pulpos", 0.88, null)
            ])
        ]);
        _iaServiceClient.Setup(c => c.IdentifyAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(iaResult);

        var dto = await _sut.CreateAsync(Guid.NewGuid(), OneImage("file.jpg", "image/jpeg", 100));

        Assert.NotNull(captured);
        Assert.Equal("completed", dto.Status);
        Assert.Single(dto.Detections);
        Assert.Equal("match", dto.Detections[0].Verdict);
        Assert.Equal("tat_00123", dto.Detections[0].Matches[0].CatalogId);
        Assert.Single(captured!.Detecciones);
        Assert.Single(captured.Detecciones.First().Coincidencias);
    }

    [Fact]
    public async Task CreateAsync_MarksNoDetection_WhenIaReturnsNoDetections()
    {
        var iaResult = new IdentifyResultDto(640, 480, "yolov8m + dinov2-vits14 + head-v1", 100, []);
        _iaServiceClient.Setup(c => c.IdentifyAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(iaResult);

        var dto = await _sut.CreateAsync(Guid.NewGuid(), OneImage("file.jpg", "image/jpeg", 100));

        Assert.Equal("no_detection", dto.Status);
        Assert.Empty(dto.Detections);
    }

    [Fact]
    public async Task CreateAsync_MarksFailed_WhenIaServiceThrows()
    {
        _iaServiceClient.Setup(c => c.IdentifyAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("boom"));

        var dto = await _sut.CreateAsync(Guid.NewGuid(), OneImage("file.jpg", "image/jpeg", 100));

        Assert.Equal("failed", dto.Status);
        _consultaRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CreateAsync_CreatesProcessingRecord_BeforeCallingIaService()
    {
        var callOrder = new List<string>();
        _consultaRepository.Setup(r => r.AddAsync(It.IsAny<Consulta>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("add"))
            .Returns(Task.CompletedTask);
        _consultaRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("save"))
            .Returns(Task.CompletedTask);
        _iaServiceClient.Setup(c => c.IdentifyAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("identify"))
            .ReturnsAsync(new IdentifyResultDto(100, 100, "v1", 10, []));

        await _sut.CreateAsync(Guid.NewGuid(), OneImage("file.jpg", "image/jpeg", 100));

        Assert.Equal(new[] { "add", "save", "identify", "save" }, callOrder);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenConsultaDoesNotExistOrIsNotOwnedByUser()
    {
        _consultaRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Consulta?)null);

        var dto = await _sut.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(dto);
    }

    [Fact]
    public async Task GetByIdAsync_EnrichesReferenceImageUrl_WhenCatalogEntryExists()
    {
        var deteccion = new Deteccion
        {
            Id = Guid.NewGuid(),
            ConsultaId = Guid.NewGuid(),
            BboxX = 10,
            BboxY = 10,
            BboxWidth = 50,
            BboxHeight = 50,
            Confianza = 0.9,
            Verdict = RTattooAI.Domain.Enums.Verdict.Match
        };
        deteccion.Coincidencias.Add(new Coincidencia
        {
            Id = Guid.NewGuid(),
            DeteccionId = deteccion.Id,
            CatalogoCodigo = "tat_00123",
            Banda = "Los Pulpos",
            Similitud = 0.88,
            ReferenceImageUrl = null
        });

        var consulta = new Consulta
        {
            Id = Guid.NewGuid(),
            UsuarioId = Guid.NewGuid(),
            Codigo = "CON-1",
            ImagenOrigUrl = "consultas/1/original.jpg",
            ImagenOrigHash = "hash",
            Estado = RTattooAI.Domain.Enums.AnalysisStatus.Completed
        };
        consulta.Detecciones.Add(deteccion);

        _consultaRepository.Setup(r => r.GetByIdAsync(consulta.Id, (Guid?)consulta.UsuarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consulta);
        _tatuajeCatalogoRepository.Setup(r => r.GetImagePathsByCodigosAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["tat_00123"] = "catalogo/tat_00123.jpg" });

        var dto = await _sut.GetByIdAsync(consulta.Id, consulta.UsuarioId);

        Assert.NotNull(dto);
        var match = dto!.Detections.Single().Matches.Single();
        Assert.Equal("https://blob.test/catalogo/tat_00123.jpg", match.ReferenceImageUrl);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsSummaries_WithTopMatch_OrderedAsReturnedByRepository()
    {
        var usuarioId = Guid.NewGuid();

        var deteccion = new Deteccion
        {
            Id = Guid.NewGuid(),
            ConsultaId = Guid.NewGuid(),
            BboxX = 0,
            BboxY = 0,
            BboxWidth = 10,
            BboxHeight = 10,
            Confianza = 0.7,
            Verdict = RTattooAI.Domain.Enums.Verdict.Match
        };
        deteccion.Coincidencias.Add(new Coincidencia
        {
            Id = Guid.NewGuid(),
            DeteccionId = deteccion.Id,
            CatalogoCodigo = "tat_00001",
            Banda = "Los Pulpos",
            Similitud = 0.65
        });
        deteccion.Coincidencias.Add(new Coincidencia
        {
            Id = Guid.NewGuid(),
            DeteccionId = deteccion.Id,
            CatalogoCodigo = "tat_00002",
            Banda = "Barrio 18",
            Similitud = 0.92
        });

        var consultaConMatch = new Consulta
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Codigo = "CON-1",
            ImagenOrigUrl = "consultas/1/original.jpg",
            ImagenOrigHash = "hash1",
            Estado = RTattooAI.Domain.Enums.AnalysisStatus.Completed
        };
        consultaConMatch.Detecciones.Add(deteccion);

        var consultaSinMatch = new Consulta
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Codigo = "CON-2",
            ImagenOrigUrl = "consultas/2/original.jpg",
            ImagenOrigHash = "hash2",
            Estado = RTattooAI.Domain.Enums.AnalysisStatus.NoDetection
        };

        _consultaRepository.Setup(r => r.GetPagedAsync(
                (Guid?)usuarioId, It.IsAny<AnalysisHistoryQuery>(), It.IsAny<AnalysisStatus?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(([consultaConMatch, consultaSinMatch], 2));

        var page = await _sut.GetHistoryAsync(usuarioId);
        var summaries = page.Items;

        Assert.Equal(2, page.Total);
        Assert.Equal(2, summaries.Count);
        Assert.Equal("Barrio 18", summaries[0].TopBand);
        Assert.Equal(0.92, summaries[0].TopSimilarity);
        Assert.Equal("completed", summaries[0].Status);
        Assert.Null(summaries[1].TopBand);
        Assert.Null(summaries[1].TopSimilarity);
        Assert.Equal("no_detection", summaries[1].Status);
    }

    [Fact]
    public async Task EnrollDetectionsAsync_ReturnsNull_WhenAnalysisNotFoundOrNotOwnedByUser()
    {
        _consultaRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Consulta?)null);

        var result = await _sut.EnrollDetectionsAsync(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()]);

        Assert.Null(result);
    }

    [Fact]
    public async Task EnrollDetectionsAsync_ThrowsInvalidRequestException_WhenNoDetectionIdsProvided()
    {
        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            _sut.EnrollDetectionsAsync(Guid.NewGuid(), Guid.NewGuid(), []));
    }

    [Fact]
    public async Task EnrollDetectionsAsync_CropsAndEnrollsSelectedDetections_WithNoBandAndOriginLinkedToAnalysis()
    {
        var usuarioId = Guid.NewGuid();
        var deteccionSeleccionada = new Deteccion
        {
            Id = Guid.NewGuid(),
            ConsultaId = Guid.NewGuid(),
            BboxX = 10,
            BboxY = 20,
            BboxWidth = 100,
            BboxHeight = 80,
            Confianza = 0.91,
            Verdict = RTattooAI.Domain.Enums.Verdict.NoMatch
        };
        var deteccionNoSeleccionada = new Deteccion
        {
            Id = Guid.NewGuid(),
            ConsultaId = deteccionSeleccionada.ConsultaId,
            BboxX = 0,
            BboxY = 0,
            BboxWidth = 30,
            BboxHeight = 30,
            Confianza = 0.4,
            Verdict = RTattooAI.Domain.Enums.Verdict.NoMatch
        };

        var consulta = new Consulta
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Codigo = "CON-1",
            ImagenOrigUrl = "consultas/1/original.jpg",
            ImagenOrigHash = "hash"
        };

        // Cada deteccion se recorta de la imagen en la que aparece, no de la portada.
        var imagen = new ConsultaImagen
        {
            Id = Guid.NewGuid(),
            ConsultaId = consulta.Id,
            Orden = 0,
            ImagenUrl = consulta.ImagenOrigUrl,
            ImagenHash = "hash",
            Origen = OrigenImagen.Archivo
        };
        consulta.Imagenes.Add(imagen);

        deteccionSeleccionada.ConsultaImagenId = imagen.Id;
        deteccionNoSeleccionada.ConsultaImagenId = imagen.Id;
        consulta.Detecciones.Add(deteccionSeleccionada);
        consulta.Detecciones.Add(deteccionNoSeleccionada);

        _consultaRepository.Setup(r => r.GetByIdAsync(consulta.Id, (Guid?)usuarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consulta);
        _blobStorage.Setup(b => b.DownloadAsync(consulta.ImagenOrigUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2, 3]);
        _imageCropper.Setup(c => c.CropToPng(It.IsAny<byte[]>(), 10, 20, 100, 80))
            .Returns([9, 9, 9]);
        _iaServiceClient.Setup(c => c.EnrollAsync(It.IsAny<Stream>(), It.IsAny<string>(), "image/png", string.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrollResultDto("tat_00099", string.Empty));

        TatuajeCatalogo? captured = null;
        _tatuajeCatalogoRepository.Setup(r => r.AddAsync(It.IsAny<TatuajeCatalogo>(), It.IsAny<CancellationToken>()))
            .Callback<TatuajeCatalogo, CancellationToken>((t, _) => captured = t)
            .Returns(Task.CompletedTask);

        var result = await _sut.EnrollDetectionsAsync(usuarioId, consulta.Id, [deteccionSeleccionada.Id]);

        Assert.NotNull(result);
        Assert.Single(result!);
        Assert.Equal("tat_00099", result[0].CatalogId);
        Assert.Null(result[0].Band);
        Assert.Equal(consulta.Id, result[0].OriginAnalysisId);
        Assert.NotNull(captured);
        Assert.Null(captured!.BandaId);
        Assert.Equal(consulta.Id, captured.ConsultaOrigenId);
        _tatuajeCatalogoRepository.Verify(r => r.AddAsync(It.IsAny<TatuajeCatalogo>(), It.IsAny<CancellationToken>()), Times.Once);
        _tatuajeCatalogoRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidateDetectionAsync_ReturnsNull_WhenAnalysisNotFoundOrNotOwnedByUser()
    {
        _consultaRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Consulta?)null);

        var result = await _sut.ValidateDetectionAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "validated", null);

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateDetectionAsync_ReturnsNull_WhenDetectionDoesNotBelongToAnalysis()
    {
        var consulta = new Consulta
        {
            Id = Guid.NewGuid(),
            UsuarioId = Guid.NewGuid(),
            Codigo = "CON-1",
            ImagenOrigUrl = "consultas/1/original.jpg",
            ImagenOrigHash = "hash"
        };
        _consultaRepository.Setup(r => r.GetByIdAsync(consulta.Id, (Guid?)consulta.UsuarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consulta);

        var result = await _sut.ValidateDetectionAsync(consulta.UsuarioId, consulta.Id, Guid.NewGuid(), "validated", null);

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateDetectionAsync_ThrowsInvalidRequestException_WhenStatusIsInvalid()
    {
        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            _sut.ValidateDetectionAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "bogus", null));

        _consultaRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ValidateDetectionAsync_UpdatesEstadoAndNotas_AndReturnsDto_WhenFound()
    {
        var usuarioId = Guid.NewGuid();
        var deteccion = new Deteccion
        {
            Id = Guid.NewGuid(),
            ConsultaId = Guid.NewGuid(),
            BboxX = 0,
            BboxY = 0,
            BboxWidth = 10,
            BboxHeight = 10,
            Confianza = 0.7,
            Verdict = RTattooAI.Domain.Enums.Verdict.Match
        };
        var consulta = new Consulta
        {
            Id = deteccion.ConsultaId,
            UsuarioId = usuarioId,
            Codigo = "CON-1",
            ImagenOrigUrl = "consultas/1/original.jpg",
            ImagenOrigHash = "hash"
        };
        consulta.Detecciones.Add(deteccion);

        _consultaRepository.Setup(r => r.GetByIdAsync(consulta.Id, (Guid?)usuarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consulta);

        var dto = await _sut.ValidateDetectionAsync(usuarioId, consulta.Id, deteccion.Id, "dismissed", "  No es un tatuaje real.  ");

        Assert.NotNull(dto);
        Assert.Equal("dismissed", dto!.ValidationStatus);
        Assert.Equal("No es un tatuaje real.", dto.ValidationNotes);
        Assert.Equal(RTattooAI.Domain.Enums.EstadoValidacion.Descartado, deteccion.EstadoValidacion);
        Assert.Equal(usuarioId, deteccion.ValidadoPorId);
        Assert.NotNull(deteccion.FechaValidacion);
        _consultaRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

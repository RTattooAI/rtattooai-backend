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

/// <summary>
/// Reintento de un análisis fallido y alcance por rol. Antes, una consulta en estado
/// `failed` solo podía rehacerse volviendo a cargar la imagen, y ningún supervisor
/// podía abrir el análisis de un analista para atender su segunda revisión.
/// </summary>
public class AnalysisServiceRetryTests
{
    private readonly Mock<IConsultaRepository> _consultaRepository = new();
    private readonly Mock<ITatuajeCatalogoRepository> _tatuajeCatalogoRepository = new();
    private readonly Mock<IBlobStorage> _blobStorage = new();
    private readonly Mock<IIaServiceClient> _iaServiceClient = new();
    private readonly Mock<IImageCropper> _imageCropper = new();
    private readonly AnalysisService _sut;

    public AnalysisServiceRetryTests()
    {
        _blobStorage.Setup(b => b.GetReadUrl(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns<string, TimeSpan>((path, _) => $"/api/files/{path}");
        _blobStorage.Setup(b => b.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestImages.Jpeg());
        _tatuajeCatalogoRepository.Setup(r => r.GetImagePathsByCodigosAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _sut = new AnalysisService(_consultaRepository.Object, _tatuajeCatalogoRepository.Object,
            _blobStorage.Object, _iaServiceClient.Object, _imageCropper.Object, NullLogger<AnalysisService>.Instance);
    }

    /// <summary>Consulta con una imagen, que es la forma minima valida tras el multi-imagen.</summary>
    private static Consulta CreateConsulta(AnalysisStatus estado, Guid? usuarioId = null)
    {
        var consulta = new Consulta
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId ?? Guid.NewGuid(),
            Codigo = "CON-1",
            ImagenOrigUrl = "consultas/1/original.jpg",
            ImagenOrigHash = "hash",
            Estado = estado
        };

        consulta.Imagenes.Add(new ConsultaImagen
        {
            Id = Guid.NewGuid(),
            ConsultaId = consulta.Id,
            Orden = 0,
            ImagenUrl = consulta.ImagenOrigUrl,
            ImagenHash = "hash",
            Estado = estado,
            Origen = OrigenImagen.Archivo
        });

        return consulta;
    }

    [Fact]
    public async Task RetryAsync_ReturnsNull_WhenAnalysisNotFoundOrNotOwnedByUser()
    {
        _consultaRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Consulta?)null);

        var result = await _sut.RetryAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
        _iaServiceClient.Verify(c => c.IdentifyAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RetryAsync_Throws_WhenAnalysisDidNotFail()
    {
        var consulta = CreateConsulta(AnalysisStatus.Completed);
        _consultaRepository.Setup(r => r.GetByIdAsync(consulta.Id, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(consulta);

        await Assert.ThrowsAsync<InvalidRequestException>(() => _sut.RetryAsync(consulta.UsuarioId, consulta.Id));

        _iaServiceClient.Verify(c => c.IdentifyAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RetryAsync_ReprocessesTheStoredImage_AndCompletesTheAnalysis()
    {
        var consulta = CreateConsulta(AnalysisStatus.Failed);
        _consultaRepository.Setup(r => r.GetByIdAsync(consulta.Id, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(consulta);
        _iaServiceClient.Setup(c => c.IdentifyAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentifyResultDto(1080, 1920, "modelo-v1", 500,
            [
                new IdentifyDetectionDto(new IdentifyBboxDto(1, 2, 3, 4), 0.9, "match",
                [
                    new IdentifyMatchDto("tat_00123", "Los Pulpos", 0.88, null)
                ])
            ]));

        var dto = await _sut.RetryAsync(consulta.UsuarioId, consulta.Id);

        Assert.NotNull(dto);
        Assert.Equal("completed", dto!.Status);
        Assert.Single(dto.Detections);
        Assert.Equal(AnalysisStatus.Completed, consulta.Estado);
        // No se vuelve a subir nada: se reutiliza la imagen ya almacenada.
        _blobStorage.Verify(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RetryAsync_KeepsFailedState_WhenIaServiceIsStillDown()
    {
        var consulta = CreateConsulta(AnalysisStatus.Failed);
        _consultaRepository.Setup(r => r.GetByIdAsync(consulta.Id, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(consulta);
        _iaServiceClient.Setup(c => c.IdentifyAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("servicio caido"));

        var dto = await _sut.RetryAsync(consulta.UsuarioId, consulta.Id);

        Assert.NotNull(dto);
        Assert.Equal("failed", dto!.Status);
    }

    [Fact]
    public async Task GetByIdAsync_DropsTheOwnerFilter_WhenTheCallerCanSupervise()
    {
        var consulta = CreateConsulta(AnalysisStatus.Completed);
        _consultaRepository.Setup(r => r.GetByIdAsync(consulta.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consulta);

        var dto = await _sut.GetByIdAsync(consulta.Id, Guid.NewGuid(), canViewAll: true);

        Assert.NotNull(dto);
        _consultaRepository.Verify(r => r.GetByIdAsync(consulta.Id, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_KeepsTheOwnerFilter_ForAnAnalyst()
    {
        var usuarioId = Guid.NewGuid();
        var consulta = CreateConsulta(AnalysisStatus.Completed, usuarioId);
        _consultaRepository.Setup(r => r.GetByIdAsync(consulta.Id, (Guid?)usuarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consulta);

        await _sut.GetByIdAsync(consulta.Id, usuarioId);

        _consultaRepository.Verify(r => r.GetByIdAsync(consulta.Id, (Guid?)usuarioId, It.IsAny<CancellationToken>()), Times.Once);
        _consultaRepository.Verify(r => r.GetByIdAsync(consulta.Id, null, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExportCsvAsync_WritesOneRowPerDetection_WithItsValidationState()
    {
        var consulta = CreateConsulta(AnalysisStatus.Completed);
        consulta.TiempoProcMs = 540;
        consulta.ModelVersion = "modelo-v1";
        var imagen = consulta.Imagenes.First();
        var deteccion = new Deteccion
        {
            Id = Guid.NewGuid(),
            ConsultaId = consulta.Id,
            ConsultaImagenId = imagen.Id,
            Confianza = 0.91,
            Verdict = Verdict.Match,
            EstadoValidacion = EstadoValidacion.Validado,
            ObservacionesPerito = "Coincide con el expediente"
        };
        deteccion.Coincidencias.Add(new Coincidencia
        {
            Id = Guid.NewGuid(),
            DeteccionId = deteccion.Id,
            CatalogoCodigo = "tat_00123",
            Banda = "Los Pulpos",
            Similitud = 0.88
        });
        consulta.Detecciones.Add(deteccion);
        imagen.Detecciones.Add(deteccion);

        _consultaRepository.Setup(r => r.GetFilteredAsync(
                null, It.IsAny<AnalysisHistoryQuery>(), It.IsAny<AnalysisStatus?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([consulta]);

        var csv = await _sut.ExportCsvAsync(Guid.NewGuid(), includeAllUsers: true);

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);                      // cabecera + una detección
        Assert.Contains("estado_validacion", lines[0]);
        Assert.Contains("Los Pulpos", lines[1]);
        Assert.Contains("validated", lines[1]);
        Assert.Contains("0.8800", lines[1]);
    }

    [Fact]
    public async Task ExportCsvAsync_NeutralisesFormulasInFreeText()
    {
        var consulta = CreateConsulta(AnalysisStatus.Completed);
        consulta.PersonaNombre = "=HYPERLINK(\"http://malicioso\")";
        _consultaRepository.Setup(r => r.GetFilteredAsync(
                null, It.IsAny<AnalysisHistoryQuery>(), It.IsAny<AnalysisStatus?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([consulta]);

        var csv = await _sut.ExportCsvAsync(Guid.NewGuid(), includeAllUsers: true);

        Assert.Contains("\"'=HYPERLINK", csv);
    }
}

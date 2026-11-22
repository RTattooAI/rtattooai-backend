using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Constants;
using RTattooAI.Application.Dtos.Analyses;
using RTattooAI.Application.Dtos.Ia;
using RTattooAI.Application.Services;
using RTattooAI.Domain.Entities;
using RTattooAI.Domain.Enums;
using Xunit;

namespace RTattooAI.Application.Tests.Services;

/// <summary>
/// Qué clase de coincidencia declara el sistema.
///
/// Son tres situaciones que antes se presentaban como dos: coincidencia firme con
/// una banda, reencuentro con un tatuaje ya visto pero sin clasificar, y candidato
/// razonable que no llega al umbral. Confundirlas lleva al perito a atribuir una
/// pertenencia que el sistema nunca afirmó.
/// </summary>
public class MatchTierTests
{
    private readonly Mock<IConsultaRepository> _consultaRepository = new();
    private readonly Mock<ITatuajeCatalogoRepository> _tatuajeCatalogoRepository = new();
    private readonly Mock<IBlobStorage> _blobStorage = new();
    private readonly Mock<IIaServiceClient> _iaServiceClient = new();
    private readonly Mock<IImageCropper> _imageCropper = new();
    private readonly AnalysisService _sut;

    public MatchTierTests()
    {
        _blobStorage.Setup(b => b.GetReadUrl(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns<string, TimeSpan>((path, _) => $"https://blob.test/{path}");
        _tatuajeCatalogoRepository.Setup(r => r.GetImagePathsByCodigosAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        _sut = new AnalysisService(
            _consultaRepository.Object, _tatuajeCatalogoRepository.Object, _blobStorage.Object,
            _iaServiceClient.Object, _imageCropper.Object, NullLogger<AnalysisService>.Instance);
    }

    private Consulta ConsultaCon(Verdict verdict, string banda)
    {
        var consultaId = Guid.NewGuid();
        var imagenId = Guid.NewGuid();

        var consulta = new Consulta
        {
            Id = consultaId,
            UsuarioId = Guid.NewGuid(),
            Codigo = "CON-1",
            ImagenOrigUrl = "consultas/1/original.jpg",
            ImagenOrigHash = "hash",
            Estado = AnalysisStatus.Completed
        };

        consulta.Imagenes.Add(new ConsultaImagen
        {
            Id = imagenId,
            ConsultaId = consultaId,
            Orden = 0,
            ImagenUrl = "consultas/1/0/original.jpg",
            ImagenHash = "hash",
            Estado = AnalysisStatus.Completed
        });

        var deteccion = new Deteccion
        {
            Id = Guid.NewGuid(),
            ConsultaId = consultaId,
            ConsultaImagenId = imagenId,
            Confianza = 0.9,
            Verdict = verdict
        };
        deteccion.Coincidencias.Add(new Coincidencia
        {
            Id = Guid.NewGuid(),
            DeteccionId = deteccion.Id,
            CatalogoCodigo = "tat_00123",
            Banda = banda,
            Similitud = 0.86
        });

        consulta.Detecciones.Add(deteccion);
        consulta.Imagenes.First().Detecciones.Add(deteccion);
        return consulta;
    }

    private async Task<DetectionDto> DetectionDtoDe(Consulta consulta)
    {
        _consultaRepository.Setup(r => r.GetByIdAsync(consulta.Id, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(consulta);

        var dto = await _sut.GetByIdAsync(consulta.Id, consulta.UsuarioId);
        return dto!.Detections.Single();
    }

    [Fact]
    public async Task UnaCoincidenciaConBandaSeDeclaraComoTal()
    {
        var dto = await DetectionDtoDe(ConsultaCon(Verdict.Match, "Los Pulpos"));

        Assert.Equal("match", dto.Verdict);
        Assert.Equal(MatchTypes.Band, dto.MatchType);
    }

    [Fact]
    public async Task UnaCoincidenciaConEntradaSinBandaEsUnReencuentro_NoUnaPertenencia()
    {
        var dto = await DetectionDtoDe(ConsultaCon(Verdict.Match, ""));

        Assert.Equal("match", dto.Verdict);
        Assert.Equal(MatchTypes.Unclassified, dto.MatchType);
    }

    [Fact]
    public async Task LaFranjaDeRevisionNoSePresentaComoCoincidencia()
    {
        var dto = await DetectionDtoDe(ConsultaCon(Verdict.Review, "Los Pulpos"));

        Assert.Equal("review", dto.Verdict);
        Assert.Equal(MatchTypes.Review, dto.MatchType);
    }

    [Fact]
    public async Task SinCoincidenciaNoSeDeclaraNingunTipo()
    {
        var dto = await DetectionDtoDe(ConsultaCon(Verdict.NoMatch, "Los Pulpos"));

        Assert.Equal("no_match", dto.Verdict);
        Assert.Equal(MatchTypes.None, dto.MatchType);
    }

    [Fact]
    public async Task EnElHistorialUnReencuentroSinBandaSeEtiquetaEnVezDeQuedarEnBlanco()
    {
        var consulta = ConsultaCon(Verdict.Match, "");
        _consultaRepository.Setup(r => r.GetPagedAsync(
                It.IsAny<Guid?>(), It.IsAny<AnalysisHistoryQuery>(), It.IsAny<AnalysisStatus?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(([consulta], 1));

        var pagina = await _sut.GetHistoryAsync(consulta.UsuarioId);

        // Una cadena vacía se vería en la tabla igual que "sin coincidencia".
        Assert.Equal(AnalysisService.SinClasificar, pagina.Items.Single().TopBand);
    }
}

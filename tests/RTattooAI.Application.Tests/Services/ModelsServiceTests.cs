using Moq;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Ia;
using RTattooAI.Application.Dtos.Models;
using RTattooAI.Application.Exceptions;
using RTattooAI.Application.Services;
using RTattooAI.Domain.Entities;
using Xunit;

namespace RTattooAI.Application.Tests.Services;

/// <summary>Versionado de modelos y sus métricas (RF-16).</summary>
public class ModelsServiceTests
{
    private const string Identificador = "yolov8m + dinov2-vits14 + head-v1";

    private readonly Mock<IVersionModeloRepository> _repository = new();
    private readonly Mock<IIaServiceClient> _iaServiceClient = new();
    private readonly ModelsService _sut;

    public ModelsServiceTests()
    {
        _repository.Setup(r => r.CountConsultasPorVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>());
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        _sut = new ModelsService(_repository.Object, _iaServiceClient.Object);
    }

    private static IaInfoDto Runtime(string version = Identificador) => new(
        version, "yolov8m_best.onnx", "dinov2-vits14", "embed_head.pt", 256,
        0.55, 0.05, 0.25, 0.45, 150, 25, 5);

    private static VersionModelo Version(string identificador = Identificador) => new()
    {
        Id = Guid.NewGuid(),
        Identificador = identificador,
        MetricaMap50 = 0.915,
        MetricaTop1 = 0.982
    };

    [Fact]
    public async Task GetInventoryAsync_ReportsRuntimeAsNull_WhenTheInferenceServiceIsDown()
    {
        _iaServiceClient.Setup(c => c.GetInfoAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IaInfoDto?)null);
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Version()]);

        var inventory = await _sut.GetInventoryAsync();

        // El histórico debe seguir consultándose aunque la inferencia no responda.
        Assert.Null(inventory.Runtime);
        Assert.Single(inventory.Versions);
    }

    [Fact]
    public async Task GetInventoryAsync_RegistersAVersionReportedByTheServiceThatWasNotKnown()
    {
        _iaServiceClient.Setup(c => c.GetInfoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Runtime("modelo-v2"));
        _repository.Setup(r => r.GetByIdentificadorAsync("modelo-v2", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VersionModelo?)null);

        VersionModelo? registrada = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<VersionModelo>(), It.IsAny<CancellationToken>()))
            .Callback<VersionModelo, CancellationToken>((v, _) => registrada = v);

        await _sut.GetInventoryAsync();

        Assert.NotNull(registrada);
        Assert.Equal("modelo-v2", registrada!.Identificador);
        Assert.Equal(0.55, registrada.Umbral);
        Assert.True(registrada.EsActiva);
    }

    [Fact]
    public async Task GetInventoryAsync_DoesNotDuplicateAnAlreadyRegisteredVersion()
    {
        _iaServiceClient.Setup(c => c.GetInfoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Runtime());
        _repository.Setup(r => r.GetByIdentificadorAsync(Identificador, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Version());

        await _sut.GetInventoryAsync();

        _repository.Verify(r => r.AddAsync(It.IsAny<VersionModelo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetInventoryAsync_MarksTheRunningVersionAsActiveAndCountsItsAnalyses()
    {
        _iaServiceClient.Setup(c => c.GetInfoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Runtime());
        _repository.Setup(r => r.GetByIdentificadorAsync(Identificador, It.IsAny<CancellationToken>())).ReturnsAsync(Version());
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Version(), Version("modelo-anterior")]);
        _repository.Setup(r => r.CountConsultasPorVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int> { [Identificador] = 31, ["modelo-anterior"] = 4 });

        var inventory = await _sut.GetInventoryAsync();

        var activa = inventory.Versions.Single(v => v.Identifier == Identificador);
        var anterior = inventory.Versions.Single(v => v.Identifier == "modelo-anterior");

        Assert.True(activa.Active);
        Assert.Equal(31, activa.AnalysesCount);
        Assert.False(anterior.Active);
        Assert.Equal(4, anterior.AnalysesCount);
        Assert.Equal(150, inventory.Runtime!.GallerySize);
    }

    [Fact]
    public async Task UpsertAsync_RejectsAnEmptyIdentifier()
    {
        await Assert.ThrowsAsync<InvalidRequestException>(() => _sut.UpsertAsync(new SaveModelVersionRequestDto("  ")));
    }

    [Fact]
    public async Task UpsertAsync_CreatesTheVersion_WhenItIsNotRegisteredYet()
    {
        _repository.Setup(r => r.GetByIdentificadorAsync("modelo-v3", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VersionModelo?)null);

        VersionModelo? creada = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<VersionModelo>(), It.IsAny<CancellationToken>()))
            .Callback<VersionModelo, CancellationToken>((v, _) => creada = v);

        var dto = await _sut.UpsertAsync(new SaveModelVersionRequestDto("modelo-v3", Map50: 0.87, Top1: 0.95, Source: "reentreno DeMSI"));

        Assert.NotNull(creada);
        Assert.Equal(0.87, dto.Map50);
        Assert.Equal("reentreno DeMSI", dto.Source);
    }

    [Fact]
    public async Task UpsertAsync_UpdatesMetricsWithoutErasingWhatWasNotSent()
    {
        var version = Version();
        _repository.Setup(r => r.GetByIdentificadorAsync(Identificador, It.IsAny<CancellationToken>())).ReturnsAsync(version);

        var dto = await _sut.UpsertAsync(new SaveModelVersionRequestDto(Identificador, Top5: 0.994));

        Assert.Equal(0.994, dto.Top5);
        Assert.Equal(0.915, dto.Map50);   // seguía registrado y no se pierde
        _repository.Verify(r => r.AddAsync(It.IsAny<VersionModelo>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

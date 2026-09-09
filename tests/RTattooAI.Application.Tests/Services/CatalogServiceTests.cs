using Moq;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Ia;
using RTattooAI.Application.Exceptions;
using RTattooAI.Application.Services;
using RTattooAI.Application.Tests.TestData;
using RTattooAI.Domain.Entities;
using Xunit;

namespace RTattooAI.Application.Tests.Services;

public class CatalogServiceTests
{
    private readonly Mock<ITatuajeCatalogoRepository> _tatuajeCatalogoRepository = new();
    private readonly Mock<IBandaCriminalRepository> _bandaCriminalRepository = new();
    private readonly Mock<IBlobStorage> _blobStorage = new();
    private readonly Mock<IIaServiceClient> _iaServiceClient = new();
    private readonly CatalogService _sut;

    public CatalogServiceTests()
    {
        _blobStorage.Setup(b => b.GetReadUrl(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns<string, TimeSpan>((path, _) => $"https://blob.test/{path}");

        _sut = new CatalogService(_tatuajeCatalogoRepository.Object, _bandaCriminalRepository.Object, _blobStorage.Object, _iaServiceClient.Object);
    }

    private static MemoryStream CreateImageStream() => TestImages.JpegStream();

    [Fact]
    public async Task EnrollAsync_ThrowsInvalidImageException_WhenContentTypeIsNotAllowed()
    {
        await Assert.ThrowsAsync<InvalidImageException>(() =>
            _sut.EnrollAsync(Guid.NewGuid(), CreateImageStream(), "file.txt", "text/plain", 100, "Los Pulpos"));

        _tatuajeCatalogoRepository.Verify(r => r.AddAsync(It.IsAny<TatuajeCatalogo>(), It.IsAny<CancellationToken>()), Times.Never);
        _iaServiceClient.Verify(c => c.EnrollAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnrollAsync_ThrowsInvalidImageException_WhenBandIsEmpty()
    {
        await Assert.ThrowsAsync<InvalidImageException>(() =>
            _sut.EnrollAsync(Guid.NewGuid(), CreateImageStream(), "file.jpg", "image/jpeg", 100, "   "));

        _iaServiceClient.Verify(c => c.EnrollAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnrollAsync_PersistsTatuajeCatalogo_AndReturnsDto_WhenValid()
    {
        var usuarioId = Guid.NewGuid();
        var banda = new BandaCriminal { Id = Guid.NewGuid(), Nombre = "Los Pulpos" };

        _iaServiceClient.Setup(c => c.EnrollAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "Los Pulpos", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrollResultDto("tat_00890", "Los Pulpos"));
        _bandaCriminalRepository.Setup(r => r.GetOrCreateByNombreAsync("Los Pulpos", usuarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(banda);

        TatuajeCatalogo? captured = null;
        _tatuajeCatalogoRepository.Setup(r => r.AddAsync(It.IsAny<TatuajeCatalogo>(), It.IsAny<CancellationToken>()))
            .Callback<TatuajeCatalogo, CancellationToken>((t, _) => captured = t)
            .Returns(Task.CompletedTask);

        var dto = await _sut.EnrollAsync(usuarioId, CreateImageStream(), "file.jpg", "image/jpeg", 100, "Los Pulpos");

        Assert.NotNull(captured);
        Assert.Equal("tat_00890", captured!.Codigo);
        Assert.Equal(banda.Id, captured.BandaId);
        Assert.Equal(usuarioId, captured.RegistradoPorId);
        Assert.False(captured.Validado);
        Assert.Equal("carga_manual", captured.Fuente);

        Assert.Equal("tat_00890", dto.CatalogId);
        Assert.Equal("Los Pulpos", dto.Band);
        Assert.False(dto.Validated);
        Assert.Equal(usuarioId, dto.AddedBy);
        _tatuajeCatalogoRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignBandAsync_ThrowsInvalidRequestException_WhenBandIsEmpty()
    {
        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            _sut.AssignBandAsync("tat_00001", "   ", Guid.NewGuid()));

        _tatuajeCatalogoRepository.Verify(r => r.GetByCodigoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssignBandAsync_ReturnsNull_WhenCatalogItemNotFound()
    {
        _tatuajeCatalogoRepository.Setup(r => r.GetByCodigoAsync("tat_99999", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TatuajeCatalogo?)null);

        var result = await _sut.AssignBandAsync("tat_99999", "Los Pulpos", Guid.NewGuid());

        Assert.Null(result);
        _bandaCriminalRepository.Verify(r => r.GetOrCreateByNombreAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssignBandAsync_UpdatesBandaId_AndReturnsDto_WhenFound()
    {
        var usuarioId = Guid.NewGuid();
        var tatuaje = new TatuajeCatalogo
        {
            Id = Guid.NewGuid(),
            Codigo = "tat_00026",
            BandaId = null,
            ImagenUrl = "catalogo/26/original.jpg",
            ImagenHash = "hash",
            Fuente = "deteccion_analisis",
            Validado = false,
            RegistradoPorId = Guid.NewGuid(),
            ConsultaOrigenId = Guid.NewGuid()
        };
        var banda = new BandaCriminal { Id = Guid.NewGuid(), Nombre = "Los Pulpos" };

        _tatuajeCatalogoRepository.Setup(r => r.GetByCodigoAsync("tat_00026", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tatuaje);
        _bandaCriminalRepository.Setup(r => r.GetOrCreateByNombreAsync("Los Pulpos", usuarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(banda);

        var dto = await _sut.AssignBandAsync("tat_00026", "Los Pulpos", usuarioId);

        Assert.NotNull(dto);
        Assert.Equal(banda.Id, tatuaje.BandaId);
        Assert.Equal("Los Pulpos", dto!.Band);
        Assert.Equal(tatuaje.ConsultaOrigenId, dto.OriginAnalysisId);
        _tatuajeCatalogoRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsMappedCatalogItems()
    {
        var banda = new BandaCriminal { Id = Guid.NewGuid(), Nombre = "Barrio 18" };
        var tatuaje = new TatuajeCatalogo
        {
            Id = Guid.NewGuid(),
            Codigo = "tat_00001",
            BandaId = banda.Id,
            Banda = banda,
            ImagenUrl = "catalogo/1/original.jpg",
            ImagenHash = "hash",
            Fuente = "carga_manual",
            Validado = true,
            RegistradoPorId = Guid.NewGuid()
        };

        _tatuajeCatalogoRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([tatuaje]);

        var items = await _sut.GetAllAsync();

        var item = Assert.Single(items);
        Assert.Equal("tat_00001", item.CatalogId);
        Assert.Equal("Barrio 18", item.Band);
        Assert.Equal("https://blob.test/catalogo/1/original.jpg", item.ImageUrl);
        Assert.True(item.Validated);
    }

    [Fact]
    public async Task SetValidatedAsync_ReturnsNull_WhenCatalogItemNotFound()
    {
        _tatuajeCatalogoRepository.Setup(r => r.GetByCodigoAsync("tat_inexistente", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TatuajeCatalogo?)null);

        var result = await _sut.SetValidatedAsync("tat_inexistente", true, Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task SetValidatedAsync_Throws_WhenTheItemHasNoBandAssigned()
    {
        // Validar una entrada sin banda dejaria el catalogo con un tatuaje "confirmado"
        // que no apunta a ninguna organizacion: no tiene sentido pericial.
        var tatuaje = new TatuajeCatalogo
        {
            Id = Guid.NewGuid(), Codigo = "tat_00900", BandaId = null,
            ImagenUrl = "catalogo/1/original.png", ImagenHash = "h", Fuente = "deteccion_analisis"
        };
        _tatuajeCatalogoRepository.Setup(r => r.GetByCodigoAsync("tat_00900", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tatuaje);

        await Assert.ThrowsAsync<InvalidRequestException>(() => _sut.SetValidatedAsync("tat_00900", true, Guid.NewGuid()));

        Assert.False(tatuaje.Validado);
    }

    [Fact]
    public async Task SetValidatedAsync_MarksTheItemAsValidated_WhenItHasABand()
    {
        var banda = new BandaCriminal { Id = Guid.NewGuid(), Nombre = "Los Pulpos" };
        var tatuaje = new TatuajeCatalogo
        {
            Id = Guid.NewGuid(), Codigo = "tat_00901", BandaId = banda.Id, Banda = banda,
            ImagenUrl = "catalogo/2/original.png", ImagenHash = "h", Fuente = "carga_manual"
        };
        _tatuajeCatalogoRepository.Setup(r => r.GetByCodigoAsync("tat_00901", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tatuaje);

        var dto = await _sut.SetValidatedAsync("tat_00901", true, Guid.NewGuid());

        Assert.NotNull(dto);
        Assert.True(dto!.Validated);
        Assert.True(tatuaje.Validado);
        Assert.Equal("Los Pulpos", dto.Band);
        _tatuajeCatalogoRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnrollAsync_Throws_WhenBytesAreNotARealImage()
    {
        await Assert.ThrowsAsync<InvalidImageException>(() =>
            _sut.EnrollAsync(Guid.NewGuid(), new MemoryStream(TestImages.NotAnImage()), "file.jpg", "image/jpeg", 100, "Los Pulpos"));

        _iaServiceClient.Verify(c => c.EnrollAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

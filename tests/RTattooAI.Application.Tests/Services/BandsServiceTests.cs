using Moq;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Bands;
using RTattooAI.Application.Exceptions;
using RTattooAI.Application.Services;
using RTattooAI.Domain.Entities;
using Xunit;

namespace RTattooAI.Application.Tests.Services;

/// <summary>Mantenimiento de bandas criminales (RF-10).</summary>
public class BandsServiceTests
{
    private readonly Mock<IBandaCriminalRepository> _repository = new();
    private readonly BandsService _sut;

    public BandsServiceTests()
    {
        _repository.Setup(r => r.CountTatuajesPorBandaAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int>());

        _sut = new BandsService(_repository.Object);
    }

    private static BandaCriminal Banda(string nombre = "Los Pulpos", Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Nombre = nombre
    };

    [Fact]
    public async Task CreateAsync_Throws_WhenNameIsEmpty()
    {
        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            _sut.CreateAsync(new SaveBandRequestDto("   "), Guid.NewGuid()));

        _repository.Verify(r => r.AddAsync(It.IsAny<BandaCriminal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("altísimo")]
    [InlineData("critical")]
    [InlineData("5")]
    public async Task CreateAsync_Throws_WhenDangerLevelIsNotOneOfTheThreeAllowed(string nivel)
    {
        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            _sut.CreateAsync(new SaveBandRequestDto("Los Pulpos", DangerLevel: nivel), Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenNameAlreadyExists()
    {
        _repository.Setup(r => r.GetByNombreAsync("Los Pulpos", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Banda());

        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            _sut.CreateAsync(new SaveBandRequestDto("Los Pulpos"), Guid.NewGuid()));

        _repository.Verify(r => r.AddAsync(It.IsAny<BandaCriminal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_NormalisesDangerLevelAndTrimsText()
    {
        BandaCriminal? guardada = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<BandaCriminal>(), It.IsAny<CancellationToken>()))
            .Callback<BandaCriminal, CancellationToken>((b, _) => guardada = b);

        var dto = await _sut.CreateAsync(
            new SaveBandRequestDto("  Los Pulpos  ", "Pulpos", " La Libertad ", "ALTO", " Extorsión "),
            Guid.NewGuid());

        Assert.NotNull(guardada);
        Assert.Equal("Los Pulpos", guardada!.Nombre);
        Assert.Equal("La Libertad", guardada.Region);
        Assert.Equal("alto", guardada.NivelPeligro);
        Assert.Equal("Extorsión", guardada.Descripcion);
        Assert.True(dto.Active);
        Assert.Equal(0, dto.TattooCount);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenBandDoesNotExist()
    {
        var result = await _sut.UpdateAsync(Guid.NewGuid(), new SaveBandRequestDto("Los Pulpos"));

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenRenamingOntoAnotherExistingBand()
    {
        var banda = Banda("Los Pulpos");
        _repository.Setup(r => r.GetByIdAsync(banda.Id, It.IsAny<CancellationToken>())).ReturnsAsync(banda);
        _repository.Setup(r => r.GetByNombreAsync("Barrio 18", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Banda("Barrio 18"));

        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            _sut.UpdateAsync(banda.Id, new SaveBandRequestDto("Barrio 18")));
    }

    [Fact]
    public async Task UpdateAsync_AllowsKeepingItsOwnName()
    {
        var banda = Banda("Los Pulpos");
        _repository.Setup(r => r.GetByIdAsync(banda.Id, It.IsAny<CancellationToken>())).ReturnsAsync(banda);
        _repository.Setup(r => r.GetByNombreAsync("Los Pulpos", It.IsAny<CancellationToken>())).ReturnsAsync(banda);

        var dto = await _sut.UpdateAsync(banda.Id, new SaveBandRequestDto("Los Pulpos", Region: "Trujillo"));

        Assert.NotNull(dto);
        Assert.Equal("Trujillo", dto!.Region);
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenBandStillHasCatalogEntries()
    {
        var banda = Banda();
        _repository.Setup(r => r.GetByIdAsync(banda.Id, It.IsAny<CancellationToken>())).ReturnsAsync(banda);
        _repository.Setup(r => r.CountTatuajesPorBandaAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [banda.Id] = 4 });

        var ex = await Assert.ThrowsAsync<ConflictException>(() => _sut.DeleteAsync(banda.Id));

        Assert.Contains("4", ex.Message);
        _repository.Verify(r => r.Remove(It.IsAny<BandaCriminal>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_RemovesBand_WhenItHasNoCatalogEntries()
    {
        var banda = Banda();
        _repository.Setup(r => r.GetByIdAsync(banda.Id, It.IsAny<CancellationToken>())).ReturnsAsync(banda);

        var borrada = await _sut.DeleteAsync(banda.Id);

        Assert.True(borrada);
        _repository.Verify(r => r.Remove(banda), Times.Once);
    }

    [Fact]
    public async Task SetActiveAsync_DeactivatesWithoutDeleting()
    {
        var banda = Banda();
        _repository.Setup(r => r.GetByIdAsync(banda.Id, It.IsAny<CancellationToken>())).ReturnsAsync(banda);

        var dto = await _sut.SetActiveAsync(banda.Id, active: false);

        Assert.NotNull(dto);
        Assert.False(dto!.Active);
        _repository.Verify(r => r.Remove(It.IsAny<BandaCriminal>()), Times.Never);
    }

    [Fact]
    public async Task GetAllAsync_ReportsHowManyTattoosEachBandHas()
    {
        var pulpos = Banda("Los Pulpos");
        var barrio = Banda("Barrio 18");
        _repository.Setup(r => r.GetAllAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pulpos, barrio]);
        _repository.Setup(r => r.CountTatuajesPorBandaAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [pulpos.Id] = 7 });

        var bandas = await _sut.GetAllAsync();

        Assert.Equal(7, bandas.Single(b => b.Name == "Los Pulpos").TattooCount);
        Assert.Equal(0, bandas.Single(b => b.Name == "Barrio 18").TattooCount);
    }
}

using Moq;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Constants;
using RTattooAI.Application.Dtos.Catalog;
using RTattooAI.Application.Dtos.Ia;
using RTattooAI.Application.Services;
using RTattooAI.Application.Tests.TestData;
using RTattooAI.Domain.Entities;
using Xunit;

namespace RTattooAI.Application.Tests.Services;

/// <summary>Campos descriptivos del catálogo: categoría, ubicación corporal y etiquetas (RF-09).</summary>
public class CatalogMetadataTests
{
    private readonly Mock<ITatuajeCatalogoRepository> _tatuajeCatalogoRepository = new();
    private readonly Mock<IBandaCriminalRepository> _bandaCriminalRepository = new();
    private readonly Mock<IBlobStorage> _blobStorage = new();
    private readonly Mock<IIaServiceClient> _iaServiceClient = new();
    private readonly CatalogService _sut;

    public CatalogMetadataTests()
    {
        _blobStorage.Setup(b => b.GetReadUrl(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns<string, TimeSpan>((path, _) => $"https://blob.test/{path}");
        _iaServiceClient.Setup(c => c.EnrollAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrollResultDto("tat_00042", "Los Pulpos"));
        _bandaCriminalRepository.Setup(r => r.GetOrCreateByNombreAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BandaCriminal { Id = Guid.NewGuid(), Nombre = "Los Pulpos" });

        _sut = new CatalogService(_tatuajeCatalogoRepository.Object, _bandaCriminalRepository.Object, _blobStorage.Object, _iaServiceClient.Object);
    }

    private static TatuajeCatalogo Existente() => new()
    {
        Id = Guid.NewGuid(),
        Codigo = "tat_00042",
        ImagenUrl = "catalogo/1/original.jpg",
        ImagenHash = "hash",
        Fuente = "carga_manual",
        BandaId = Guid.NewGuid()
    };

    [Fact]
    public async Task EnrollAsync_StoresTheDescriptiveFields()
    {
        TatuajeCatalogo? guardado = null;
        _tatuajeCatalogoRepository.Setup(r => r.AddAsync(It.IsAny<TatuajeCatalogo>(), It.IsAny<CancellationToken>()))
            .Callback<TatuajeCatalogo, CancellationToken>((t, _) => guardado = t);

        var dto = await _sut.EnrollAsync(
            Guid.NewGuid(), TestImages.JpegStream(), "tatuaje.jpg", "image/jpeg", 100, "Los Pulpos",
            new SaveCatalogMetadataRequestDto("Corona de tres puntas", "Tinta negra", "Símbolo", "Antebrazo derecho", "Corona, PUNTAS ,corona"));

        Assert.NotNull(guardado);
        Assert.Equal("Símbolo", guardado!.Categoria);
        Assert.Equal("Antebrazo derecho", guardado.UbicacionCorporal);
        Assert.Equal("corona,puntas", guardado.Tags);
        Assert.Equal(["corona", "puntas"], dto.Tags);
        Assert.Equal("Corona de tres puntas", dto.Name);
    }

    [Fact]
    public async Task EnrollAsync_WithoutMetadata_LeavesTheFieldsEmpty()
    {
        TatuajeCatalogo? guardado = null;
        _tatuajeCatalogoRepository.Setup(r => r.AddAsync(It.IsAny<TatuajeCatalogo>(), It.IsAny<CancellationToken>()))
            .Callback<TatuajeCatalogo, CancellationToken>((t, _) => guardado = t);

        await _sut.EnrollAsync(Guid.NewGuid(), TestImages.JpegStream(), "t.jpg", "image/jpeg", 100, "Los Pulpos");

        Assert.NotNull(guardado);
        Assert.Null(guardado!.Categoria);
        Assert.Null(guardado.Tags);
    }

    [Fact]
    public async Task UpdateMetadataAsync_ReturnsNull_WhenTheEntryDoesNotExist()
    {
        var result = await _sut.UpdateMetadataAsync("tat_99999", new SaveCatalogMetadataRequestDto("x"));

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateMetadataAsync_ReplacesTheFieldsAndPersists()
    {
        var tatuaje = Existente();
        _tatuajeCatalogoRepository.Setup(r => r.GetByCodigoAsync("tat_00042", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tatuaje);

        var dto = await _sut.UpdateMetadataAsync(
            "tat_00042", new SaveCatalogMetadataRequestDto(Category: "Letras", BodyLocation: "Cuello", Tags: "gótica"));

        Assert.NotNull(dto);
        Assert.Equal("Letras", tatuaje.Categoria);
        Assert.Equal("Cuello", tatuaje.UbicacionCorporal);
        Assert.Equal(["gótica"], dto!.Tags);
        _tatuajeCatalogoRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_WithAQuery_AsksTheRepositoryToFilter()
    {
        _tatuajeCatalogoRepository.Setup(r => r.GetFilteredAsync(It.IsAny<CatalogQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Existente()]);

        var items = await _sut.GetAllAsync(new CatalogQuery(Category: "Símbolo"));

        Assert.Single(items);
        _tatuajeCatalogoRepository.Verify(r => r.GetFilteredAsync(It.IsAny<CatalogQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        _tatuajeCatalogoRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetFacetsAsync_MergesTheStartingVocabularyWithWhatIsAlreadyUsed()
    {
        var uno = Existente();
        uno.Categoria = "Símbolo";
        uno.UbicacionCorporal = "Antebrazo";
        uno.Tags = "corona,puntas";
        uno.Banda = new BandaCriminal { Id = Guid.NewGuid(), Nombre = "Los Pulpos" };

        var dos = Existente();
        dos.Categoria = "Símbolo";
        dos.Tags = "corona";

        _tatuajeCatalogoRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([uno, dos]);

        _bandaCriminalRepository.Setup(r => r.GetAllAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var facets = await _sut.GetFacetsAsync();

        // Categorías y ubicaciones combinan el vocabulario inicial con lo usado: el
        // primer registro necesita de dónde elegir, y lo que alguien escriba nuevo
        // tiene que quedar disponible para el siguiente.
        Assert.Contains("Símbolo", facets.Categories);
        Assert.Contains("Corona", facets.Categories);          // del vocabulario inicial
        Assert.Contains("Antebrazo", facets.BodyLocations);    // usado, no predefinido
        Assert.Contains("Cuello", facets.BodyLocations);       // del vocabulario inicial
        Assert.DoesNotContain("Antebrazo", CatalogVocabulary.UbicacionesCorporales);

        // Las bandas y las etiquetas salen solo de los datos: inventar bandas sería
        // meter ruido en lo único que el sistema afirma.
        Assert.Equal(["Los Pulpos"], facets.Bands);
        Assert.Equal(["corona", "puntas"], facets.Tags);
    }

    [Fact]
    public async Task GetFacetsAsync_OffersTheStartingVocabularyWithAnEmptyCatalogue()
    {
        _tatuajeCatalogoRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _bandaCriminalRepository.Setup(r => r.GetAllAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var facets = await _sut.GetFacetsAsync();

        Assert.Equal(CatalogVocabulary.Categorias.Count, facets.Categories.Count);
        Assert.Equal(CatalogVocabulary.UbicacionesCorporales.Count, facets.BodyLocations.Count);
        Assert.Empty(facets.Bands);
        Assert.Empty(facets.Tags);
    }

    [Fact]
    public async Task GetFacetsAsync_OffersRegisteredBandsEvenWithoutTattoosYet()
    {
        _tatuajeCatalogoRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _bandaCriminalRepository.Setup(r => r.GetAllAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new BandaCriminal { Id = Guid.NewGuid(), Nombre = "Barrio 18" }]);

        var facets = await _sut.GetFacetsAsync();

        // Una banda recién dada de alta debe poder elegirse al enrolar el primer
        // tatuaje; si solo salieran las que ya tienen catálogo, nunca aparecería.
        Assert.Equal(["Barrio 18"], facets.Bands);
    }
}

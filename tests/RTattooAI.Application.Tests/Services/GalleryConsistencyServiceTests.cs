using Moq;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Ia;
using RTattooAI.Application.Exceptions;
using RTattooAI.Application.Services;
using RTattooAI.Domain.Entities;
using Xunit;

namespace RTattooAI.Application.Tests.Services;

/// <summary>
/// El estado de partida reproduce el despliegue del 14/09/2026: el catálogo tiene al
/// payaso como tat_00026, pero en la galería ese código guarda los vectores del tatuaje
/// que se enroló después como tat_00027, y ambos comparten un vector.
/// </summary>
public class GalleryConsistencyServiceTests
{
    private readonly Mock<ITatuajeCatalogoRepository> _catalogo = new();
    private readonly Mock<IIaServiceClient> _ia = new();
    private readonly Mock<IBlobStorage> _almacenamiento = new();
    private readonly GalleryConsistencyService _sut;

    public GalleryConsistencyServiceTests()
    {
        _sut = new GalleryConsistencyService(_catalogo.Object, _ia.Object, _almacenamiento.Object);
        _almacenamiento.Setup(b => b.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([1, 2, 3]);
        _ia.Setup(c => c.EnrollAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream _, string _, string _, string banda, string? codigo, CancellationToken _) => new EnrollResultDto(codigo!, banda));
        _ia.Setup(c => c.RemoveFromGalleryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private static TatuajeCatalogo Ficha(string codigo, string hash) =>
        new() { Codigo = codigo, ImagenUrl = $"catalogo/{codigo}/original.png", ImagenHash = hash, Fuente = "carga_manual" };

    private void CatalogoDelIncidente() =>
        _catalogo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            Ficha("tat_00001", "h-tren"),
            Ficha("tat_00026", "h-payaso"),
            Ficha("tat_00027", "h-amigo"),
            Ficha("tat_00030", "h-sin-vectores")
        ]);

    private static GalleryStateDto GaleriaDelIncidente() => new(
        [
            new GalleryEntryDto("tat_00001", 17, null),
            new GalleryEntryDto("tat_00026", 5, "h-amigo"),
            new GalleryEntryDto("tat_00027", 5, "h-amigo"),
            new GalleryEntryDto("tat_00099", 5, "h-huerfano")
        ],
        [["tat_00026", "tat_00027"]]);

    private static GalleryStateDto GaleriaReparada() => new(
        [
            new GalleryEntryDto("tat_00001", 17, null),
            new GalleryEntryDto("tat_00026", 5, "h-payaso"),
            new GalleryEntryDto("tat_00027", 5, "h-amigo"),
            new GalleryEntryDto("tat_00030", 5, "h-sin-vectores"),
            // El huérfano sigue ahí: reparar nunca lo borra (ver el incidente del 15/09).
            new GalleryEntryDto("tat_00099", 5, "h-huerfano")
        ],
        []);

    [Fact]
    public async Task CheckAsync_FindsEveryKindOfMismatch()
    {
        CatalogoDelIncidente();
        _ia.Setup(c => c.GetGalleryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(GaleriaDelIncidente());

        var estado = await _sut.CheckAsync();

        Assert.True(estado.ServiceAvailable);
        Assert.False(estado.Consistent);
        Assert.Equal(["tat_00030"], estado.MissingInGallery);
        Assert.Equal(["tat_00099"], estado.OrphansInGallery);
        // tat_00026 tiene vectores cuya huella es la de la imagen de tat_00027.
        Assert.Equal(["tat_00026"], estado.ImageMismatch);
        Assert.Equal(["tat_00026", "tat_00027"], Assert.Single(estado.SharedVectors));
        Assert.Equal(1, estado.Unverified);
    }

    [Fact]
    public async Task CheckAsync_SaysSo_WhenTheInferenceServiceIsDown()
    {
        CatalogoDelIncidente();
        _ia.Setup(c => c.GetGalleryAsync(It.IsAny<CancellationToken>())).ReturnsAsync((GalleryStateDto?)null);

        var estado = await _sut.CheckAsync();

        Assert.False(estado.ServiceAvailable);
        Assert.False(estado.Consistent);
    }

    [Fact]
    public async Task RepairAsync_ReenrollsFromTheStoredImage_ButNeverRemovesOrphans()
    {
        CatalogoDelIncidente();
        _ia.SetupSequence(c => c.GetGalleryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(GaleriaDelIncidente())
            .ReturnsAsync(GaleriaReparada());

        var resultado = await _sut.RepairAsync();

        Assert.Equal(["tat_00026", "tat_00027", "tat_00030"], resultado.Reenrolled);
        // Nunca se borra un huérfano como parte de la reparación (ver el incidente del 15/09:
        // borró las 25 identidades base porque el catálogo de ese despliegue nunca las tuvo).
        Assert.Empty(resultado.Removed);
        Assert.Empty(resultado.Failed);
        Assert.Equal(["tat_00099"], resultado.After.OrphansInGallery);

        // Antes de volver a enrolar se quitan los vectores equivocados de un código que SÍ
        // está en el catálogo — eso no es borrar un huérfano, es corregir su propio código.
        _ia.Verify(c => c.RemoveFromGalleryAsync("tat_00026", It.IsAny<CancellationToken>()), Times.Once);
        _ia.Verify(c => c.RemoveFromGalleryAsync("tat_00027", It.IsAny<CancellationToken>()), Times.Once);
        _ia.Verify(c => c.RemoveFromGalleryAsync("tat_00030", It.IsAny<CancellationToken>()), Times.Never);
        // El huérfano jamás se pasa a RemoveFromGalleryAsync.
        _ia.Verify(c => c.RemoveFromGalleryAsync("tat_00099", It.IsAny<CancellationToken>()), Times.Never);
        _almacenamiento.Verify(b => b.DownloadAsync("catalogo/tat_00026/original.png", It.IsAny<CancellationToken>()), Times.Once);
        // La identidad base sin huella no se toca: sus vectores salen de varias imágenes del dataset.
        _ia.Verify(c => c.EnrollAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), "tat_00001", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RepairAsync_NeverRemovesOrphans_EvenWhenTheyAreMostOfTheGallery()
    {
        // Reproduce el incidente del 15/09/2026: un catálogo con 2 fichas y una galería
        // con 27 identidades, 25 de ellas sin ficha porque nunca se sembraron en SQL.
        // "Sin ficha" no es "sobra": no debe borrarse ni una.
        _catalogo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            Ficha("tat_00026", "h-payaso"),
            Ficha("tat_00027", "h-amigo")
        ]);

        var identidadesBase = Enumerable.Range(1, 25)
            .Select(i => new GalleryEntryDto($"tat_{i:00000}", 17, null));
        var galeria = new GalleryStateDto(
            [.. identidadesBase, new GalleryEntryDto("tat_00026", 5, "h-payaso"), new GalleryEntryDto("tat_00027", 5, "h-amigo")],
            []);
        _ia.Setup(c => c.GetGalleryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(galeria);

        var resultado = await _sut.RepairAsync();

        Assert.Empty(resultado.Removed);
        Assert.Empty(resultado.Reenrolled);
        _ia.Verify(c => c.RemoveFromGalleryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RepairAsync_Refuses_WhenTheCatalogIsEmpty()
    {
        // Leer mal el catálogo no puede terminar borrando la galería entera.
        _catalogo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _ia.Setup(c => c.GetGalleryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(GaleriaDelIncidente());

        await Assert.ThrowsAsync<InvalidRequestException>(() => _sut.RepairAsync());

        _ia.Verify(c => c.RemoveFromGalleryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RepairAsync_Refuses_WhenTheInferenceServiceIsDown()
    {
        CatalogoDelIncidente();
        _ia.Setup(c => c.GetGalleryAsync(It.IsAny<CancellationToken>())).ReturnsAsync((GalleryStateDto?)null);

        await Assert.ThrowsAsync<InvalidRequestException>(() => _sut.RepairAsync());
    }
}

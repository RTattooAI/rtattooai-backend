using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Ia;
using RTattooAI.Application.Exceptions;
using RTattooAI.Application.Services;
using RTattooAI.Application.Tests.TestData;
using RTattooAI.Domain.Entities;
using Xunit;

namespace RTattooAI.Application.Tests.Services;

/// <summary>
/// El 11/09/2026 la galería repartió a un tatuaje el código `tat_00026`, que el catálogo
/// ya tenía asignado a otro; la base rechazó la ficha y los vectores se quedaron. Desde
/// entonces el sistema presentaba ese tatuaje con la banda del primero. Estas pruebas
/// fijan las dos defensas: el código lo elige el catálogo y, si la ficha no se guarda,
/// los vectores se retiran.
/// </summary>
public class GalleryEnrollmentTests
{
    private readonly Mock<IIaServiceClient> _ia = new();

    private static TatuajeCatalogo Ficha(string codigo) =>
        new() { Codigo = codigo, ImagenUrl = $"catalogo/{codigo}/original.png", ImagenHash = "h", Fuente = "carga_manual" };

    [Fact]
    public void Next_FollowsTheHighestCatalogCode()
    {
        Assert.Equal("tat_00027", CatalogCodes.Next(["tat_00003", "tat_00026", "otro-formato"]));
    }

    [Fact]
    public void Next_StartsAtOne_WhenTheCatalogIsEmpty()
    {
        Assert.Equal("tat_00001", CatalogCodes.Next([]));
    }

    [Fact]
    public async Task EnrollWithFreshCodeAsync_AsksTheGalleryForTheCodeAfterTheCatalog()
    {
        _ia.Setup(c => c.EnrollAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "", "tat_00027", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrollResultDto("tat_00027", ""));
        var ocupados = new HashSet<string> { "tat_00026" };

        var codigo = await GalleryEnrollment.EnrollWithFreshCodeAsync(_ia.Object, ocupados, [1, 2, 3], "t.png", "image/png", "", default);

        Assert.Equal("tat_00027", codigo);
        Assert.Contains("tat_00027", ocupados);
    }

    [Fact]
    public async Task EnrollWithFreshCodeAsync_SkipsACodeThatAlreadyHasVectors()
    {
        _ia.Setup(c => c.EnrollAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "", "tat_00027", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CatalogIdTakenException("tat_00027"));
        _ia.Setup(c => c.EnrollAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "", "tat_00028", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrollResultDto("tat_00028", ""));

        var codigo = await GalleryEnrollment.EnrollWithFreshCodeAsync(
            _ia.Object, new HashSet<string> { "tat_00026" }, [1, 2, 3], "t.png", "image/png", "", default);

        Assert.Equal("tat_00028", codigo);
    }

    [Fact]
    public async Task RollbackAsync_TriesEveryCode_EvenIfOneFails()
    {
        _ia.Setup(c => c.RemoveFromGalleryAsync("tat_00001", It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException());
        _ia.Setup(c => c.RemoveFromGalleryAsync("tat_00002", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await GalleryEnrollment.RollbackAsync(_ia.Object, ["tat_00001", "tat_00002"]);

        _ia.Verify(c => c.RemoveFromGalleryAsync("tat_00002", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CatalogEnroll_RemovesTheVectors_WhenTheCatalogRowCannotBeSaved()
    {
        var repositorio = new Mock<ITatuajeCatalogoRepository>();
        var bandas = new Mock<IBandaCriminalRepository>();
        var almacenamiento = new Mock<IBlobStorage>();
        repositorio.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Ficha("tat_00026")]);
        repositorio.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("clave duplicada"));
        bandas.Setup(r => r.GetOrCreateByNombreAsync("Los Pulpos", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BandaCriminal { Id = Guid.NewGuid(), Nombre = "Los Pulpos" });
        _ia.Setup(c => c.EnrollAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "Los Pulpos", "tat_00027", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrollResultDto("tat_00027", "Los Pulpos"));

        var sut = new CatalogService(repositorio.Object, bandas.Object, almacenamiento.Object, _ia.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.EnrollAsync(Guid.NewGuid(), TestImages.JpegStream(), "t.jpg", "image/jpeg", 100, "Los Pulpos"));

        _ia.Verify(c => c.RemoveFromGalleryAsync("tat_00027", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnrollFromAnalysis_RemovesTheVectors_WhenTheCatalogRowsCannotBeSaved()
    {
        // Es el camino exacto del incidente: enrolar una detección desde un análisis.
        var usuarioId = Guid.NewGuid();
        var consulta = new Consulta { Id = Guid.NewGuid(), UsuarioId = usuarioId, Codigo = "CON-1", ImagenOrigUrl = "o.jpg", ImagenOrigHash = "h" };
        var imagen = new ConsultaImagen { Id = Guid.NewGuid(), ConsultaId = consulta.Id, ImagenUrl = "consultas/1/original.jpg", ImagenHash = "h", Origen = OrigenImagen.Camara };
        var deteccion = new Deteccion { Id = Guid.NewGuid(), ConsultaId = consulta.Id, ConsultaImagenId = imagen.Id, BboxWidth = 10, BboxHeight = 10 };
        consulta.Imagenes.Add(imagen);
        consulta.Detecciones.Add(deteccion);

        var consultas = new Mock<IConsultaRepository>();
        var catalogo = new Mock<ITatuajeCatalogoRepository>();
        var almacenamiento = new Mock<IBlobStorage>();
        var recortes = new Mock<IImageCropper>();
        consultas.Setup(r => r.GetByIdAsync(consulta.Id, (Guid?)usuarioId, It.IsAny<CancellationToken>())).ReturnsAsync(consulta);
        almacenamiento.Setup(b => b.DownloadAsync(imagen.ImagenUrl, It.IsAny<CancellationToken>())).ReturnsAsync([1, 2, 3]);
        recortes.Setup(c => c.CropToPng(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>())).Returns([4, 5, 6]);
        catalogo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Ficha("tat_00026")]);
        catalogo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("clave duplicada"));
        _ia.Setup(c => c.EnrollAsync(It.IsAny<Stream>(), It.IsAny<string>(), "image/png", string.Empty, "tat_00027", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrollResultDto("tat_00027", string.Empty));

        var sut = new AnalysisService(consultas.Object, catalogo.Object, almacenamiento.Object, _ia.Object, recortes.Object,
            NullLogger<AnalysisService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.EnrollDetectionsAsync(usuarioId, consulta.Id, [deteccion.Id]));

        _ia.Verify(c => c.RemoveFromGalleryAsync("tat_00027", It.IsAny<CancellationToken>()), Times.Once);
    }
}

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
/// Un caso real necesita varias tomas: distintas zonas del cuerpo o distintos ángulos
/// del mismo tatuaje. Cada imagen se procesa por separado y el análisis agrega el
/// resultado, de modo que una toma fallida no invalida las demás.
/// </summary>
public class AnalysisServiceMultiImageTests
{
    private readonly Mock<IConsultaRepository> _consultaRepository = new();
    private readonly Mock<ITatuajeCatalogoRepository> _tatuajeCatalogoRepository = new();
    private readonly Mock<IBlobStorage> _blobStorage = new();
    private readonly Mock<IIaServiceClient> _iaServiceClient = new();
    private readonly Mock<IImageCropper> _imageCropper = new();
    private readonly AnalysisService _sut;

    private Consulta? _capturada;

    public AnalysisServiceMultiImageTests()
    {
        _blobStorage.Setup(b => b.GetReadUrl(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns<string, TimeSpan>((path, _) => $"/api/files/{path}");
        _tatuajeCatalogoRepository.Setup(r => r.GetImagePathsByCodigosAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _imageCropper.Setup(c => c.ResizeToThumbnailPng(It.IsAny<byte[]>(), It.IsAny<int>())).Returns([1, 2, 3]);
        _consultaRepository.Setup(r => r.AddAsync(It.IsAny<Consulta>(), It.IsAny<CancellationToken>()))
            .Callback<Consulta, CancellationToken>((c, _) => _capturada = c)
            .Returns(Task.CompletedTask);

        _sut = new AnalysisService(_consultaRepository.Object, _tatuajeCatalogoRepository.Object,
            _blobStorage.Object, _iaServiceClient.Object, _imageCropper.Object, NullLogger<AnalysisService>.Instance);
    }

    private static IReadOnlyList<AnalysisImageInput> Images(int cantidad, string source = "archivo") =>
        [.. Enumerable.Range(0, cantidad).Select(i =>
            new AnalysisImageInput(TestImages.JpegStream(), $"toma-{i}.jpg", "image/jpeg", 100, source))];

    private static IdentifyResultDto ConDetecciones(int cuantas) =>
        new(1080, 1920, "modelo-v1", 500,
            [.. Enumerable.Range(0, cuantas).Select(i =>
                new IdentifyDetectionDto(new IdentifyBboxDto(i * 10, i * 10, 50, 50), 0.9, "match",
                    [new IdentifyMatchDto($"tat_{i:00000}", "Los Pulpos", 0.88, null)]))]);

    private void SetupIa(params IdentifyResultDto[] respuestas)
    {
        var cola = new Queue<IdentifyResultDto>(respuestas);
        _iaServiceClient.Setup(c => c.IdentifyAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => cola.Count > 1 ? cola.Dequeue() : cola.Peek());
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenNoImagesAreProvided()
    {
        await Assert.ThrowsAsync<InvalidImageException>(() => _sut.CreateAsync(Guid.NewGuid(), []));

        _consultaRepository.Verify(r => r.AddAsync(It.IsAny<Consulta>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenThereAreTooManyImages()
    {
        var demasiadas = Images(AnalysisService.MaxImagesPerAnalysis + 1);

        await Assert.ThrowsAsync<InvalidImageException>(() => _sut.CreateAsync(Guid.NewGuid(), demasiadas));

        _blobStorage.Verify(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_RejectsTheWholeAnalysis_WhenOneImageIsInvalid()
    {
        // Se valida todo antes de escribir: un análisis no debe quedar creado a medias.
        IReadOnlyList<AnalysisImageInput> mixtas =
        [
            new AnalysisImageInput(TestImages.JpegStream(), "buena.jpg", "image/jpeg", 100, "archivo"),
            new AnalysisImageInput(new MemoryStream(TestImages.NotAnImage()), "mala.jpg", "image/jpeg", 100, "archivo")
        ];

        await Assert.ThrowsAsync<InvalidImageException>(() => _sut.CreateAsync(Guid.NewGuid(), mixtas));

        _consultaRepository.Verify(r => r.AddAsync(It.IsAny<Consulta>(), It.IsAny<CancellationToken>()), Times.Never);
        _blobStorage.Verify(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ProcessesEveryImage_AndKeepsTheirOrder()
    {
        SetupIa(ConDetecciones(1), ConDetecciones(2), ConDetecciones(0));

        var dto = await _sut.CreateAsync(Guid.NewGuid(), Images(3));

        Assert.Equal(3, dto.Images.Count);
        Assert.Equal([0, 1, 2], dto.Images.Select(i => i.Order));
        // Una llamada al servicio de IA por imagen.
        _iaServiceClient.Verify(c => c.IdentifyAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        Assert.Equal(3, _capturada!.Imagenes.Count);
    }

    [Fact]
    public async Task CreateAsync_KeepsTheSourceOfEachImage_WhenCameraShotsAndFilesAreMixed()
    {
        // HU0020: una toma de la cámara no pierde su procedencia por compartir análisis
        // con una fotografía cargada desde el equipo.
        SetupIa(ConDetecciones(1));
        IReadOnlyList<AnalysisImageInput> mixtas =
        [
            new AnalysisImageInput(TestImages.JpegStream(), "archivo.jpg", "image/jpeg", 100, "archivo"),
            new AnalysisImageInput(TestImages.JpegStream(), "captura.jpg", "image/jpeg", 100, "camara")
        ];

        await _sut.CreateAsync(Guid.NewGuid(), mixtas);

        Assert.Equal(
            [OrigenImagen.Archivo, OrigenImagen.Camara],
            _capturada!.Imagenes.OrderBy(i => i.Orden).Select(i => i.Origen));
    }

    [Fact]
    public async Task CreateAsync_LinksEachDetectionToTheImageItCameFrom()
    {
        SetupIa(ConDetecciones(1), ConDetecciones(2));

        var dto = await _sut.CreateAsync(Guid.NewGuid(), Images(2));

        var idsDeImagen = dto.Images.Select(i => i.Id).ToHashSet();
        Assert.All(dto.Detections, d => Assert.Contains(d.ImageId, idsDeImagen));
        // La primera imagen aporta una detección y la segunda dos.
        Assert.Equal(1, dto.Detections.Count(d => d.ImageId == dto.Images[0].Id));
        Assert.Equal(2, dto.Detections.Count(d => d.ImageId == dto.Images[1].Id));
    }

    [Fact]
    public async Task CreateAsync_KeepsTheFirstImageAsTheCover()
    {
        SetupIa(ConDetecciones(1));

        var dto = await _sut.CreateAsync(Guid.NewGuid(), Images(3));

        Assert.Equal(dto.Images[0].ImageUrl, dto.ImageUrl);
        Assert.Equal(dto.Images[0].ThumbnailUrl, dto.ThumbnailUrl);
        Assert.Equal(_capturada!.Imagenes.OrderBy(i => i.Orden).First().ImagenUrl, _capturada.ImagenOrigUrl);
    }

    [Fact]
    public async Task CreateAsync_IsCompleted_WhenAtLeastOneImageHasDetections()
    {
        // La segunda imagen no detecta nada, pero el caso sigue siendo útil.
        SetupIa(ConDetecciones(1), new IdentifyResultDto(640, 480, "modelo-v1", 100, []));

        var dto = await _sut.CreateAsync(Guid.NewGuid(), Images(2));

        Assert.Equal("completed", dto.Status);
        Assert.Equal("completed", dto.Images[0].Status);
        Assert.Equal("no_detection", dto.Images[1].Status);
    }

    [Fact]
    public async Task CreateAsync_MarksOnlyTheFailingImage_WhenTheIaServiceFailsForOne()
    {
        var cola = new Queue<Func<IdentifyResultDto>>(
        [
            () => ConDetecciones(1),
            () => throw new HttpRequestException("servicio caido")
        ]);
        _iaServiceClient.Setup(c => c.IdentifyAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => cola.Dequeue()());

        var dto = await _sut.CreateAsync(Guid.NewGuid(), Images(2));

        Assert.Equal("completed", dto.Status);
        Assert.Equal("completed", dto.Images[0].Status);
        Assert.Equal("failed", dto.Images[1].Status);
    }

    [Fact]
    public async Task CreateAsync_IsFailed_WhenEveryImageFails()
    {
        _iaServiceClient.Setup(c => c.IdentifyAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("servicio caido"));

        var dto = await _sut.CreateAsync(Guid.NewGuid(), Images(2));

        Assert.Equal("failed", dto.Status);
        Assert.All(dto.Images, i => Assert.Equal("failed", i.Status));
    }

    [Fact]
    public async Task CreateAsync_RecordsWhetherEachImageCameFromTheCamera()
    {
        SetupIa(ConDetecciones(1));

        var dto = await _sut.CreateAsync(Guid.NewGuid(), Images(2, source: "camara"));

        Assert.All(dto.Images, i => Assert.Equal("camara", i.Source));
    }

    [Fact]
    public async Task CreateAsync_FallsBackToFileSource_WhenTheClientDeclaresSomethingElse()
    {
        SetupIa(ConDetecciones(1));

        var dto = await _sut.CreateAsync(Guid.NewGuid(), Images(1, source: "inventado"));

        Assert.Equal("archivo", dto.Images[0].Source);
    }

    [Fact]
    public async Task PreviewAsync_ReturnsTheDetections_WithoutPersistingAnything()
    {
        SetupIa(ConDetecciones(2));

        var preview = await _sut.PreviewAsync(
            new AnalysisImageInput(TestImages.JpegStream(), "frame.jpg", "image/jpeg", 100, "camara"));

        Assert.Equal(2, preview.Detections.Count);
        Assert.Equal("modelo-v1", preview.ModelVersion);
        Assert.Contains("NO queda registrado", preview.Disclaimer);

        // Nada de esto debe ocurrir en el modo en vivo: ni consulta, ni archivo, ni traza.
        _consultaRepository.Verify(r => r.AddAsync(It.IsAny<Consulta>(), It.IsAny<CancellationToken>()), Times.Never);
        _consultaRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _blobStorage.Verify(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PreviewAsync_Throws_WhenTheFrameIsNotARealImage()
    {
        await Assert.ThrowsAsync<InvalidImageException>(() => _sut.PreviewAsync(
            new AnalysisImageInput(new MemoryStream(TestImages.NotAnImage()), "frame.jpg", "image/jpeg", 100, "camara")));

        _iaServiceClient.Verify(c => c.IdentifyAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RetryAsync_ReprocessesOnlyTheFailedImages()
    {
        var consulta = new Consulta
        {
            Id = Guid.NewGuid(),
            UsuarioId = Guid.NewGuid(),
            Codigo = "CON-1",
            ImagenOrigUrl = "consultas/1/a/original.jpg",
            ImagenOrigHash = "hash",
            Estado = AnalysisStatus.Completed
        };
        consulta.Imagenes.Add(new ConsultaImagen
        {
            Id = Guid.NewGuid(), ConsultaId = consulta.Id, Orden = 0,
            ImagenUrl = "consultas/1/a/original.jpg", ImagenHash = "h", Estado = AnalysisStatus.Completed
        });
        consulta.Imagenes.Add(new ConsultaImagen
        {
            Id = Guid.NewGuid(), ConsultaId = consulta.Id, Orden = 1,
            ImagenUrl = "consultas/1/b/original.jpg", ImagenHash = "h", Estado = AnalysisStatus.Failed
        });

        _consultaRepository.Setup(r => r.GetByIdAsync(consulta.Id, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(consulta);
        _blobStorage.Setup(b => b.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestImages.Jpeg());
        SetupIa(ConDetecciones(1));

        var dto = await _sut.RetryAsync(consulta.UsuarioId, consulta.Id);

        Assert.NotNull(dto);
        // Solo se reprocesa la imagen fallida: la que ya estaba bien no se vuelve a enviar.
        _iaServiceClient.Verify(c => c.IdentifyAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        _blobStorage.Verify(b => b.DownloadAsync("consultas/1/b/original.jpg", It.IsAny<CancellationToken>()), Times.Once);
        _blobStorage.Verify(b => b.DownloadAsync("consultas/1/a/original.jpg", It.IsAny<CancellationToken>()), Times.Never);
    }
}

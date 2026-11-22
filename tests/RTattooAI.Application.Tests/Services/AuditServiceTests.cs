using Moq;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Audit;
using RTattooAI.Application.Services;
using RTattooAI.Domain.Entities;
using Xunit;

namespace RTattooAI.Application.Tests.Services;

/// <summary>Consulta y exportación del registro de auditoría (RF-15, RF-17).</summary>
public class AuditServiceTests
{
    private readonly Mock<IAuditRepository> _repository = new();
    private readonly AuditService _sut;

    public AuditServiceTests()
    {
        _sut = new AuditService(_repository.Object);
    }

    private static RegistroAuditoria Registro(string accion = "POST /api/analyses", string? email = "perito@pnp.gob.pe") => new()
    {
        Id = Guid.NewGuid(),
        UsuarioId = Guid.NewGuid(),
        UsuarioEmail = email,
        UsuarioRol = "analyst",
        Accion = accion,
        Recurso = "b1f2",
        StatusCode = 201,
        DireccionIp = "190.12.0.4",
        Fecha = new DateTime(2026, 9, 9, 10, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task GetAsync_ReturnsThePageAndTheTotal()
    {
        _repository.Setup(r => r.GetPagedAsync(It.IsAny<AuditQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(([Registro()], 137));

        var page = await _sut.GetAsync(new AuditQuery(Page: 2, PageSize: 10));

        Assert.Equal(137, page.Total);
        Assert.Equal(2, page.Page);
        Assert.Equal(10, page.PageSize);
        Assert.Equal(14, page.TotalPages);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task GetAsync_ClampsAnAbsurdPageSize()
    {
        AuditQuery? recibida = null;
        _repository.Setup(r => r.GetPagedAsync(It.IsAny<AuditQuery>(), It.IsAny<CancellationToken>()))
            .Callback<AuditQuery, CancellationToken>((q, _) => recibida = q)
            .ReturnsAsync(([], 0));

        var page = await _sut.GetAsync(new AuditQuery(Page: 0, PageSize: 100_000));

        Assert.NotNull(recibida);
        Assert.Equal(1, recibida!.NormalizedPage);
        Assert.Equal(50, recibida.NormalizedPageSize);
        Assert.Equal(50, page.PageSize);
    }

    [Fact]
    public async Task ExportCsvAsync_WritesAHeaderAndOneRowPerEntry()
    {
        _repository.Setup(r => r.GetFilteredAsync(It.IsAny<AuditQuery>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Registro(), Registro("DELETE /api/bands/1")]);

        var csv = await _sut.ExportCsvAsync(new AuditQuery());
        var lineas = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lineas.Length);
        Assert.Contains("fecha_utc", lineas[0]);
        Assert.Contains("2026-09-09T10:00:00Z", lineas[1]);
        Assert.Contains("perito@pnp.gob.pe", lineas[1]);
        Assert.Contains("DELETE /api/bands/1", lineas[2]);
    }

    [Fact]
    public async Task ExportCsvAsync_IsCappedSoAnUnfilteredExportCannotLoadTheWholeTable()
    {
        var tope = 0;
        _repository.Setup(r => r.GetFilteredAsync(It.IsAny<AuditQuery>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<AuditQuery, int, CancellationToken>((_, max, _) => tope = max)
            .ReturnsAsync([]);

        await _sut.ExportCsvAsync(new AuditQuery());

        Assert.Equal(AuditService.MaxExportRows, tope);
    }

    [Fact]
    public async Task RecordAsync_PersistsImmediately()
    {
        var registro = Registro();

        await _sut.RecordAsync(registro);

        _repository.Verify(r => r.AddAsync(registro, It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

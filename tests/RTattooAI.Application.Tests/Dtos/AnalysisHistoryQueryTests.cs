using RTattooAI.Application.Dtos.Analyses;
using RTattooAI.Application.Dtos.Common;
using Xunit;

namespace RTattooAI.Application.Tests.Dtos;

/// <summary>Paginación y filtros del historial (RF-13).</summary>
public class AnalysisHistoryQueryTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(4, 4)]
    public void PageNeverGoesBelowOne(int pedida, int esperada)
    {
        Assert.Equal(esperada, new AnalysisHistoryQuery(Page: pedida).NormalizedPage);
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(10_000, 25)]
    [InlineData(50, 50)]
    public void PageSizeIsCappedSoOneRequestCannotPullTheWholeTable(int pedido, int esperado)
    {
        Assert.Equal(esperado, new AnalysisHistoryQuery(PageSize: pedido).NormalizedPageSize);
    }

    [Fact]
    public void PageSizeCannotExceedItsDeclaredMaximum()
    {
        Assert.Equal(AnalysisHistoryQuery.MaxPageSize, new AnalysisHistoryQuery(PageSize: AnalysisHistoryQuery.MaxPageSize).NormalizedPageSize);
    }

    [Theory]
    [InlineData(0, 25, 0)]
    [InlineData(1, 25, 1)]
    [InlineData(25, 25, 1)]
    [InlineData(26, 25, 2)]
    public void TotalPagesRoundsUp(int total, int pageSize, int esperado)
    {
        var page = new PagedResultDto<string>([], total, 1, pageSize);

        Assert.Equal(esperado, page.TotalPages);
    }
}

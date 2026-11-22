using RTattooAI.Application.Dtos.Analyses;
using Xunit;

namespace RTattooAI.Application.Tests.Dtos;

/// <summary>
/// HU0020: las tomas de la cámara quedan registradas con procedencia "camara" aunque
/// el mismo análisis incluya fotografías cargadas desde el equipo.
/// </summary>
public class AnalysisImageSourcesTests
{
    [Fact]
    public void PerImage_KeepsTheSourceOfEachImage_WhenThereIsOnePerImage()
    {
        var origenes = AnalysisImageSources.PerImage(["archivo", "camara", "camara"], "archivo", 3);

        Assert.Equal(["archivo", "camara", "camara"], origenes);
    }

    [Fact]
    public void PerImage_UsesTheCommonSource_WhenCountsDoNotMatch()
    {
        var origenes = AnalysisImageSources.PerImage(["camara"], "archivo", 2);

        Assert.Equal(["archivo", "archivo"], origenes);
    }

    [Fact]
    public void PerImage_AcceptsTheOldSingleField_WhenSourcesIsMissing()
    {
        var origenes = AnalysisImageSources.PerImage([], "camara", 2);

        Assert.Equal(["camara", "camara"], origenes);
    }

    [Fact]
    public void PerImage_DefaultsToArchivo_WhenNothingIsSent()
    {
        var origenes = AnalysisImageSources.PerImage([], null, 1);

        Assert.Equal([AnalysisImageSources.Default], origenes);
    }

    [Fact]
    public void PerImage_FillsBlankEntriesWithTheCommonSource()
    {
        var origenes = AnalysisImageSources.PerImage(["camara", " "], "archivo", 2);

        Assert.Equal(["camara", "archivo"], origenes);
    }
}

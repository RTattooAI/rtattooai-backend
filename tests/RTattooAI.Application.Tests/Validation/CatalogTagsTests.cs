using RTattooAI.Application.Validation;
using Xunit;

namespace RTattooAI.Application.Tests.Validation;

/// <summary>Etiquetas del catálogo (RF-09).</summary>
public class CatalogTagsTests
{
    [Fact]
    public void NormalizeLowercasesTrimsAndRemovesDuplicates()
    {
        var normalizadas = CatalogTags.Normalize(" León ; corona , LEÓN ,  corona ");

        Assert.Equal("león,corona", normalizadas);
    }

    [Fact]
    public void NormalizeReturnsNullWhenThereIsNothingUsable()
    {
        Assert.Null(CatalogTags.Normalize(null));
        Assert.Null(CatalogTags.Normalize("   "));
        Assert.Null(CatalogTags.Normalize(",,;,"));
    }

    [Fact]
    public void NormalizeCapsHowManyTagsAreKept()
    {
        var muchas = string.Join(',', Enumerable.Range(1, CatalogTags.MaxTags + 5).Select(i => $"tag{i}"));

        var normalizadas = CatalogTags.Normalize(muchas);

        Assert.Equal(CatalogTags.MaxTags, CatalogTags.Split(normalizadas).Count);
    }

    [Fact]
    public void NormalizeTruncatesOverlongTags()
    {
        var larga = new string('a', CatalogTags.MaxTagLength + 20);

        var normalizadas = CatalogTags.Normalize(larga);

        Assert.Equal(CatalogTags.MaxTagLength, normalizadas!.Length);
    }

    [Fact]
    public void MatchesComparesWholeTagsNotSubstrings()
    {
        // Filtrar con LIKE haría que "león" casara con "leones"; el filtro compara etiquetas completas.
        Assert.True(CatalogTags.Matches("león,corona", "LEÓN"));
        Assert.False(CatalogTags.Matches("leones,corona", "león"));
    }

    [Fact]
    public void SplitOfNothingIsAnEmptyList()
    {
        Assert.Empty(CatalogTags.Split(null));
        Assert.Empty(CatalogTags.Split(""));
    }
}

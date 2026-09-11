using RTattooAI.Application.Constants;
using RTattooAI.Application.Validation;
using Xunit;

namespace RTattooAI.Application.Tests.Validation;

/// <summary>
/// Unificación de categorías y ubicaciones ignorando mayúsculas y tildes.
///
/// El caso que motivó esto apareció en la aplicación desplegada: el desplegable de
/// categorías ofrecía «Simbolo» y «Símbolo» como dos opciones distintas, porque el
/// vocabulario predefinido trae la forma acentuada y una ficha se había guardado sin
/// acento. Dos nombres para la misma cosa es exactamente lo que el vocabulario existe
/// para evitar.
/// </summary>
public class TextoCanonicoTests
{
    [Theory]
    [InlineData("Símbolo", "simbolo")]
    [InlineData("  SIMBOLO  ", "simbolo")]
    [InlineData("Número", "numero")]
    [InlineData("Pie o tobillo", "pie o tobillo")]
    public void ClaveIgnoraMayusculasTildesYEspacios(string entrada, string esperada)
    {
        Assert.Equal(esperada, TextoCanonico.Clave(entrada));
    }

    [Fact]
    public void ClaveDeVacioEsCadenaVacia()
    {
        Assert.Equal(string.Empty, TextoCanonico.Clave(null));
        Assert.Equal(string.Empty, TextoCanonico.Clave("   "));
    }

    [Theory]
    [InlineData("simbolo")]
    [InlineData("SÍMBOLO")]
    [InlineData(" Simbolo ")]
    public void CanonizarDevuelveLaGrafiaDelVocabulario(string escrito)
    {
        var resultado = TextoCanonico.Canonizar(escrito, CatalogVocabulary.Categorias);

        Assert.Equal("Símbolo", resultado);
    }

    [Fact]
    public void CanonizarRespetaLosValoresQueNoEstanEnElVocabulario()
    {
        // El vocabulario no es una lista cerrada: lo que no reconoce se guarda tal cual.
        var resultado = TextoCanonico.Canonizar("  Mascarón de proa  ", CatalogVocabulary.Categorias);

        Assert.Equal("Mascarón de proa", resultado);
    }

    [Fact]
    public void CanonizarDevuelveNuloCuandoNoHayValor()
    {
        Assert.Null(TextoCanonico.Canonizar(null, CatalogVocabulary.Categorias));
        Assert.Null(TextoCanonico.Canonizar("   ", CatalogVocabulary.Categorias));
    }

    [Fact]
    public void DistintosUneLasVariantesYConservaLaPrimera()
    {
        // Quien llama decide la preferencia con el orden: el vocabulario va primero,
        // de modo que la grafía que sobrevive es la suya.
        var resultado = TextoCanonico.Distintos(["Símbolo", "simbolo", "SIMBOLO", "Corona"]);

        Assert.Equal(["Corona", "Símbolo"], resultado);
    }

    [Fact]
    public void DistintosDescartaVaciosYRecortaEspacios()
    {
        var resultado = TextoCanonico.Distintos(["  Corona  ", null, "   ", "corona"]);

        Assert.Equal(["Corona"], resultado);
    }
}

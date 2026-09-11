using System.Globalization;
using System.Text;

namespace RTattooAI.Application.Validation;

/// <summary>
/// Comparación de texto que ignora mayúsculas <b>y tildes</b>.
///
/// Hace falta porque la base de datos usa la colación <c>SQL_Latin1_General_CP1_CI_AS</c>:
/// ignora mayúsculas pero no acentos, de modo que «Simbolo» y «Símbolo» son dos
/// valores distintos. Sin esto, el vocabulario predefinido y lo que ya escribió
/// alguien conviven duplicados en los desplegables, y el catálogo deja de agrupar
/// — que es justamente lo que el vocabulario venía a evitar.
///
/// Advertencia: al descomponer, la «ñ» pierde su virgulilla, así que «año» y «ano»
/// se consideran el mismo valor. Para nombres de categoría y ubicación corporal es
/// asumible, y a cambio permite encontrar «Año» escribiendo «ano».
/// </summary>
public static class TextoCanonico
{
    /// <summary>Clave de comparación: sin tildes, en minúsculas y sin espacios sobrantes.</summary>
    public static string Clave(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        var descompuesto = valor.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);

        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Devuelve la forma del vocabulario que corresponde al valor escrito, o el valor
    /// tal cual si no corresponde a ninguna. Es lo que evita que se acumulen variantes
    /// del mismo término: quien escribe «simbolo» acaba guardando «Símbolo».
    /// </summary>
    public static string? Canonizar(string? valor, IReadOnlyList<string> vocabulario)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var clave = Clave(valor);
        return vocabulario.FirstOrDefault(v => Clave(v) == clave) ?? valor.Trim();
    }

    /// <summary>
    /// Valores distintos ignorando mayúsculas y tildes, conservando la primera
    /// aparición. Quien llama decide la preferencia con el orden: si pasa primero el
    /// vocabulario, gana la grafía del vocabulario.
    /// </summary>
    public static IReadOnlyList<string> Distintos(IEnumerable<string?> valores) =>
        [.. valores
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .DistinctBy(Clave, StringComparer.Ordinal)
            .Order(StringComparer.CurrentCulture)];
}

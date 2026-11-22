namespace RTattooAI.Application.Dtos.Analyses;

/// <summary>
/// Procedencia de cada fotografía de un análisis. Un mismo caso mezcla fotografías
/// cargadas desde el equipo y tomas de la cámara, así que el formulario envía
/// `sources` repetido una vez por imagen, en el mismo orden que `images`.
///
/// `source` es el campo anterior, único para todo el análisis. Con él, una mezcla se
/// registraba entera como `archivo` y las tomas de la cámara perdían su procedencia.
/// Se sigue aceptando como valor común cuando `sources` no llega.
/// </summary>
public static class AnalysisImageSources
{
    public const string Default = "archivo";

    public static IReadOnlyList<string> PerImage(IReadOnlyList<string?> sources, string? source, int imageCount)
    {
        var comun = string.IsNullOrWhiteSpace(source) ? Default : source.Trim();

        // Si no coinciden en número no hay forma de saber qué procedencia corresponde a
        // qué imagen; adivinar registraría una carga como toma de cámara, o al revés.
        if (sources.Count != imageCount)
        {
            return [.. Enumerable.Repeat(comun, imageCount)];
        }

        return [.. sources.Select(s => string.IsNullOrWhiteSpace(s) ? comun : s.Trim())];
    }
}

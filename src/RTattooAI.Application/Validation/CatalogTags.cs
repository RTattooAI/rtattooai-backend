namespace RTattooAI.Application.Validation;

/// <summary>
/// Normalización de las etiquetas del catálogo (RF-09). Se guardan en minúsculas,
/// sin espacios sobrantes y sin repetidos, para que filtrar por etiqueta no dependa
/// de cómo las escribió cada persona.
/// </summary>
public static class CatalogTags
{
    public const int MaxTags = 12;
    public const int MaxTagLength = 30;

    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var tags = Split(raw)
            .Select(t => t.Length > MaxTagLength ? t[..MaxTagLength] : t)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxTags)
            .ToList();

        return tags.Count == 0 ? null : string.Join(',', tags);
    }

    public static IReadOnlyList<string> Split(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return [.. raw
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .Where(t => t.Length > 0)];
    }

    public static bool Matches(string? stored, string tag) =>
        Split(stored).Contains(tag.Trim().ToLowerInvariant());
}

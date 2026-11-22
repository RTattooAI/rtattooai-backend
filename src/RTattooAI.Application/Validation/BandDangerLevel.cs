namespace RTattooAI.Application.Validation;

/// <summary>
/// Nivel de peligro de una banda (RF-10). Se limita a tres valores para que el
/// dato sea agregable en el panel; texto libre lo haría inservible para métricas.
/// </summary>
public static class BandDangerLevel
{
    public const string Bajo = "bajo";
    public const string Medio = "medio";
    public const string Alto = "alto";

    public static readonly IReadOnlyList<string> All = [Bajo, Medio, Alto];

    public static bool IsValid(string? value) =>
        string.IsNullOrWhiteSpace(value) || All.Contains(value.Trim().ToLowerInvariant());

    public static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}

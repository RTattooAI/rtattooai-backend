using System.Globalization;
using System.Text;

namespace RTattooAI.Application.Export;

/// <summary>
/// Construcción de CSV para las exportaciones del sistema. Centraliza dos cuidados
/// que es fácil olvidar al escribir cada exportación por separado: el escapado de
/// comillas y la neutralización de fórmulas, para que un texto libre guardado por un
/// usuario no se ejecute al abrir el archivo en Excel.
/// </summary>
public sealed class CsvBuilder
{
    private readonly StringBuilder _buffer = new();

    public CsvBuilder(params string[] headers)
    {
        if (headers.Length > 0)
        {
            AppendRow(headers);
        }
    }

    public void AppendRow(params string?[] values)
    {
        _buffer.AppendLine(string.Join(',', values.Select(Escape)));
    }

    public override string ToString() => _buffer.ToString();

    /// <summary>Fecha en ISO-8601 UTC, el formato que espera el análisis posterior.</summary>
    public static string Iso(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    public static string Number(double? value, string format = "F4") =>
        value?.ToString(format, CultureInfo.InvariantCulture) ?? string.Empty;

    public static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // Excel y LibreOffice interpretan como fórmula todo lo que empieza por = + - @;
        // el apóstrofo inicial lo desactiva sin alterar el texto que se muestra.
        var safe = value[0] is '=' or '+' or '-' or '@' ? "'" + value : value;

        return safe.Contains('"') || safe.Contains(',') || safe.Contains('\n') || safe.Contains('\r')
            ? $"\"{safe.Replace("\"", "\"\"")}\""
            : safe;
    }
}

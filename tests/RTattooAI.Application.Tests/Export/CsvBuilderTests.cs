using RTattooAI.Application.Export;
using Xunit;

namespace RTattooAI.Application.Tests.Export;

public class CsvBuilderTests
{
    [Fact]
    public void HeaderIsTheFirstLine()
    {
        var csv = new CsvBuilder("fecha", "accion").ToString();

        Assert.StartsWith("fecha,accion", csv, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://malicioso\")")]
    [InlineData("+1+1")]
    [InlineData("-2+3")]
    [InlineData("@SUM(A1)")]
    public void FormulasAreNeutralisedWithALeadingApostrophe(string peligroso)
    {
        var csv = new CsvBuilder();
        csv.AppendRow(peligroso);

        Assert.Contains("'" + peligroso[0], csv.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ValuesWithCommasOrQuotesAreQuotedAndEscaped()
    {
        var csv = new CsvBuilder();
        csv.AppendRow("Pulpos, Trujillo", "dijo \"coincide\"");

        var linea = csv.ToString().TrimEnd();
        Assert.Equal("\"Pulpos, Trujillo\",\"dijo \"\"coincide\"\"\"", linea);
    }

    [Fact]
    public void NullAndEmptyValuesBecomeEmptyFields()
    {
        var csv = new CsvBuilder();
        csv.AppendRow(null, "", "x");

        Assert.Equal(",,x", csv.ToString().TrimEnd());
    }

    [Fact]
    public void DatesAreWrittenAsIso8601Utc()
    {
        var fecha = new DateTime(2026, 9, 9, 14, 5, 3, DateTimeKind.Utc);

        Assert.Equal("2026-09-09T14:05:03Z", CsvBuilder.Iso(fecha));
    }

    [Fact]
    public void NumbersUseInvariantCultureSoDecimalsAreNotLocalised()
    {
        Assert.Equal("0.8800", CsvBuilder.Number(0.88));
        Assert.Equal("", CsvBuilder.Number(null));
    }
}

using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace RTattooAI.Api.Extensions;

public static class CsvResults
{
    /// <summary>
    /// Devuelve un CSV descargable. El BOM UTF-8 no es opcional: sin él, Excel en
    /// Windows abre el archivo con los acentos rotos y las tablas del informe salen
    /// con "análisis" convertido en ruido.
    /// </summary>
    public static FileContentResult Csv(this ControllerBase controller, string content, string namePrefix)
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(content)).ToArray();
        var fileName = $"{namePrefix}-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv";

        return controller.File(bytes, "text/csv", fileName);
    }
}

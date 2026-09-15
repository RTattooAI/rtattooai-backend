using System.Globalization;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Exceptions;

namespace RTattooAI.Application.Services;

/// <summary>
/// Códigos de catálogo. El catálogo es la fuente de verdad: el código se calcula a
/// partir de los que existen en la base, no de los que tenga la galería.
/// </summary>
public static class CatalogCodes
{
    public const string Prefix = "tat_";

    public static string Next(IEnumerable<string> ocupados)
    {
        var mayor = 0;
        foreach (var codigo in ocupados)
        {
            if (codigo.StartsWith(Prefix, StringComparison.Ordinal)
                && int.TryParse(codigo.AsSpan(Prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var numero))
            {
                mayor = Math.Max(mayor, numero);
            }
        }

        return $"{Prefix}{mayor + 1:00000}";
    }
}

/// <summary>
/// Enrolamiento coordinado entre la galería del servicio de inferencia y el catálogo.
///
/// Las dos viven en sitios distintos y no hay transacción que las abarque. El 11/09/2026
/// eso produjo una atribución falsa: una reconstrucción de la galería había dejado
/// fuera el tatuaje `tat_00026` del catálogo, la galería volvió a dar ese código a otro
/// tatuaje, la base rechazó la ficha por código duplicado y los vectores se quedaron;
/// desde entonces el catálogo presentaba ese tatuaje con la banda del primero. Aquí se
/// cierran las dos puertas: el código lo decide el catálogo y la galería rechaza uno
/// ocupado, y si la ficha no llega a guardarse se retiran los vectores.
/// </summary>
public static class GalleryEnrollment
{
    /// <summary>Suficiente para saltar códigos huérfanos sin quedarse en bucle.</summary>
    public const int MaxIntentos = 20;

    /// <param name="ocupados">
    /// Códigos del catálogo. Se amplía con los que se van usando, de modo que varias
    /// llamadas de una misma operación no pidan dos veces el mismo.
    /// </param>
    public static async Task<string> EnrollWithFreshCodeAsync(
        IIaServiceClient iaServiceClient,
        ISet<string> ocupados,
        byte[] imagen,
        string fileName,
        string contentType,
        string band,
        CancellationToken cancellationToken)
    {
        for (var intento = 0; intento < MaxIntentos; intento++)
        {
            var codigo = CatalogCodes.Next(ocupados);
            ocupados.Add(codigo);

            try
            {
                using var stream = new MemoryStream(imagen);
                var resultado = await iaServiceClient.EnrollAsync(stream, fileName, contentType, band, codigo, cancellationToken);

                // Un servicio anterior ignora el código pedido y asigna el suyo: se usa el
                // que devuelve, que es con el que quedaron guardados los vectores.
                ocupados.Add(resultado.CatalogId);
                return resultado.CatalogId;
            }
            catch (CatalogIdTakenException)
            {
                // La galería tiene vectores con ese código que el catálogo no conoce:
                // se salta y se prueba el siguiente.
            }
        }

        throw new InvalidOperationException(
            $"No se encontró un código libre tras {MaxIntentos} intentos: la galería tiene demasiados vectores sin ficha en el catálogo.");
    }

    /// <summary>
    /// Retira de la galería lo enrolado en una operación que no llegó a guardarse. Nunca
    /// lanza: si falla, la comprobación de coherencia lo mostrará como vectores huérfanos.
    /// </summary>
    public static async Task RollbackAsync(IIaServiceClient iaServiceClient, IEnumerable<string> codigos)
    {
        foreach (var codigo in codigos)
        {
            try
            {
                await iaServiceClient.RemoveFromGalleryAsync(codigo, CancellationToken.None);
            }
            catch (Exception)
            {
                // Se sigue con el resto.
            }
        }
    }
}

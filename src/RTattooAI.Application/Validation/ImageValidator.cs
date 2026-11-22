using RTattooAI.Application.Exceptions;

namespace RTattooAI.Application.Validation;

/// <summary>Resultado de inspeccionar el contenido real de una imagen aceptada.</summary>
/// <param name="ContentType">Tipo detectado por los bytes, no el declarado por el cliente.</param>
public readonly record struct ValidatedImage(string ContentType, int Width, int Height)
{
    public string Extension => ContentType switch
    {
        "image/png" => "png",
        "image/webp" => "webp",
        _ => "jpg"
    };
}

public static class ImageValidator
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        // WEBP está en el RF-01 del proyecto y es lo que produce por defecto la captura
        // desde cámara en varios navegadores.
        "image/webp"
    };

    public const long MaxImageBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Tope de píxeles decodificados. Una imagen de 10 MB muy comprimida puede
    /// expandirse a varios GB en memoria al generar la miniatura o el recorte
    /// (decompression bomb); 80 MP cubre cualquier cámara real con margen.
    /// </summary>
    public const long MaxPixels = 80_000_000;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Primer filtro, antes de leer el archivo: tipo declarado y tamaño.</summary>
    public static void EnsureAllowedUpload(string contentType, long contentLength)
    {
        if (!AllowedContentTypes.Contains(contentType))
        {
            throw new InvalidImageException("Formato de imagen no soportado. Solo se aceptan JPG, PNG o WEBP.");
        }

        if (contentLength <= 0 || contentLength > MaxImageBytes)
        {
            throw new InvalidImageException("El archivo supera el tamaño máximo permitido de 10 MB.");
        }
    }

    /// <summary>
    /// Segundo filtro, con el archivo ya en memoria: comprueba que los bytes sean
    /// realmente JPG, PNG o WEBP y que las dimensiones estén dentro del presupuesto.
    /// El tipo declarado por el cliente no se vuelve a usar a partir de aquí.
    /// </summary>
    public static ValidatedImage EnsureValidContent(ReadOnlySpan<byte> content)
    {
        if (content.Length <= 0 || content.Length > MaxImageBytes)
        {
            throw new InvalidImageException("El archivo supera el tamaño máximo permitido de 10 MB.");
        }

        var image = TryReadPng(content) ?? TryReadJpeg(content) ?? TryReadWebp(content)
            ?? throw new InvalidImageException("El archivo no es una imagen JPG, PNG o WEBP válida.");

        if (image.Width <= 0 || image.Height <= 0)
        {
            throw new InvalidImageException("El archivo no es una imagen JPG, PNG o WEBP válida.");
        }

        if ((long)image.Width * image.Height > MaxPixels)
        {
            throw new InvalidImageException("La imagen excede la resolución máxima admitida.");
        }

        return image;
    }

    private static ValidatedImage? TryReadPng(ReadOnlySpan<byte> content)
    {
        // 8 bytes de firma + 4 de longitud + "IHDR" + ancho/alto en big-endian.
        if (content.Length < 24 || !content[..8].SequenceEqual(PngSignature))
        {
            return null;
        }

        if (content[12] != (byte)'I' || content[13] != (byte)'H' || content[14] != (byte)'D' || content[15] != (byte)'R')
        {
            return null;
        }

        return new ValidatedImage("image/png", ReadInt32BigEndian(content[16..20]), ReadInt32BigEndian(content[20..24]));
    }

    /// <summary>
    /// WEBP se envuelve en un contenedor RIFF: "RIFF" + tamaño + "WEBP" + un chunk que
    /// indica la variante (VP8 con pérdida, VP8L sin pérdida o VP8X extendido), y cada
    /// variante codifica las dimensiones de forma distinta.
    /// </summary>
    private static ValidatedImage? TryReadWebp(ReadOnlySpan<byte> content)
    {
        if (content.Length < 30
            || !content[..4].SequenceEqual("RIFF"u8)
            || !content[8..12].SequenceEqual("WEBP"u8))
        {
            return null;
        }

        var chunk = content[12..16];

        if (chunk.SequenceEqual("VP8 "u8))
        {
            // Cabecera del keyframe VP8: 3 bytes de tag, la firma 9D 01 2A y 14 bits por dimensión.
            if (content.Length < 30 || content[23] != 0x9D || content[24] != 0x01 || content[25] != 0x2A)
            {
                return null;
            }

            var ancho = (content[26] | (content[27] << 8)) & 0x3FFF;
            var alto = (content[28] | (content[29] << 8)) & 0x3FFF;
            return new ValidatedImage("image/webp", ancho, alto);
        }

        if (chunk.SequenceEqual("VP8L"u8))
        {
            // 14 bits de ancho y 14 de alto, menos uno, empaquetados tras la firma 0x2F.
            if (content.Length < 25 || content[20] != 0x2F)
            {
                return null;
            }

            var bits = content[21] | (content[22] << 8) | (content[23] << 16) | (content[24] << 24);
            return new ValidatedImage("image/webp", (bits & 0x3FFF) + 1, ((bits >> 14) & 0x3FFF) + 1);
        }

        if (chunk.SequenceEqual("VP8X"u8))
        {
            // Canvas extendido: dimensiones menos uno en 24 bits little-endian.
            if (content.Length < 30)
            {
                return null;
            }

            var ancho = (content[24] | (content[25] << 8) | (content[26] << 16)) + 1;
            var alto = (content[27] | (content[28] << 8) | (content[29] << 16)) + 1;
            return new ValidatedImage("image/webp", ancho, alto);
        }

        return null;
    }

    private static ValidatedImage? TryReadJpeg(ReadOnlySpan<byte> content)
    {
        if (content.Length < 4 || content[0] != 0xFF || content[1] != 0xD8 || content[2] != 0xFF)
        {
            return null;
        }

        var offset = 2;
        while (offset + 3 < content.Length)
        {
            if (content[offset] != 0xFF)
            {
                offset++;   // relleno entre segmentos
                continue;
            }

            var marker = content[offset + 1];
            offset += 2;

            // Marcadores sin carga útil (relleno, RSTn, SOI, EOI).
            if (marker is 0xFF or 0x01 or >= 0xD0 and <= 0xD9)
            {
                continue;
            }

            if (offset + 1 >= content.Length)
            {
                break;
            }

            var segmentLength = (content[offset] << 8) | content[offset + 1];
            if (segmentLength < 2 || offset + segmentLength > content.Length)
            {
                break;
            }

            // SOF0-SOF3, SOF5-SOF7, SOF9-SOF11, SOF13-SOF15 llevan las dimensiones.
            var isStartOfFrame = marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC);
            if (isStartOfFrame && offset + 7 < content.Length)
            {
                var height = (content[offset + 3] << 8) | content[offset + 4];
                var width = (content[offset + 5] << 8) | content[offset + 6];
                return new ValidatedImage("image/jpeg", width, height);
            }

            if (marker == 0xDA)
            {
                break;      // inicio de los datos comprimidos: ya no hay cabeceras
            }

            offset += segmentLength;
        }

        return null;
    }

    private static int ReadInt32BigEndian(ReadOnlySpan<byte> bytes) =>
        (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
}

using RTattooAI.Application.Exceptions;

namespace RTattooAI.Application.Validation;

public static class ImageValidator
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png"
    };

    private const long MaxImageBytes = 10 * 1024 * 1024;

    public static void EnsureValid(string contentType, long contentLength)
    {
        if (!AllowedContentTypes.Contains(contentType))
        {
            throw new InvalidImageException("Formato de imagen no soportado. Solo se aceptan JPG o PNG.");
        }

        if (contentLength <= 0 || contentLength > MaxImageBytes)
        {
            throw new InvalidImageException("El archivo supera el tamaño máximo permitido de 10 MB.");
        }
    }
}

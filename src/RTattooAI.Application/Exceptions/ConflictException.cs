namespace RTattooAI.Application.Exceptions;

/// <summary>
/// La petición es válida pero choca con el estado actual del sistema: borrar una
/// banda que todavía tiene tatuajes asociados, por ejemplo. Se traduce a 409.
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }
}

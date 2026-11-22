namespace RTattooAI.Application.Exceptions;

/// <summary>
/// El usuario intenta una consulta nueva sin haber validado las detecciones de la
/// anterior. Se traduce a 409 con el id de la consulta pendiente.
/// </summary>
public class PendingValidationException : Exception
{
    public PendingValidationException(Guid pendingAnalysisId)
        : base("Valide las detecciones de su consulta anterior antes de realizar una nueva.")
    {
        PendingAnalysisId = pendingAnalysisId;
    }

    public Guid PendingAnalysisId { get; }
}

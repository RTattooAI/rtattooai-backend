namespace RTattooAI.Domain.Enums;

public enum Verdict
{
    /// <summary>Por encima del umbral firme: el sistema sostiene la coincidencia.</summary>
    Match,

    /// <summary>Por debajo del umbral de revisión: ninguna coincidencia conocida.</summary>
    NoMatch,

    /// <summary>
    /// Franja intermedia. Hay un candidato razonable pero no lo bastante firme como
    /// para afirmarlo. Un tatuaje gastado cae aquí con frecuencia: presentarlo como
    /// "sin coincidencia" pierde información útil, y presentarlo como coincidencia
    /// afirma algo que el sistema no puede sostener. Se marca para que lo mire una
    /// persona, que es exactamente lo que esta herramienta dice ser.
    /// </summary>
    Review
}

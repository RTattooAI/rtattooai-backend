namespace RTattooAI.Application.Constants;

/// <summary>
/// Qué clase de coincidencia encontró el sistema. El veredicto (`match` /
/// `no_match`) dice si hay parecido suficiente; esto dice de qué sirve.
///
/// La distinción importa porque el catálogo contiene dos cosas distintas:
/// tatuajes atribuidos a una banda, y tatuajes que alguien registró desde un
/// análisis y que todavía nadie ha clasificado. Reencontrarse con uno de estos
/// segundos no significa "pertenece a una banda", significa "este tatuaje ya se
/// vio antes en otro caso" — que es información útil por sí sola y que hasta
/// ahora el sistema tenía y no decía.
/// </summary>
public static class MatchTypes
{
    /// <summary>Coincide con una entrada que tiene banda asignada.</summary>
    public const string Band = "band";

    /// <summary>
    /// Coincide con una entrada del catálogo que aún no tiene banda: el sistema ya
    /// había visto este tatuaje, pero nadie ha determinado a qué corresponde.
    /// </summary>
    public const string Unclassified = "unclassified";

    /// <summary>
    /// Hay un candidato razonable pero por debajo del umbral firme. El sistema no
    /// lo afirma: lo pone delante de una persona para que decida.
    /// </summary>
    public const string Review = "review";

    /// <summary>No hay coincidencia por encima del umbral.</summary>
    public const string None = "none";
}

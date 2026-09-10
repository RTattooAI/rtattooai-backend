namespace RTattooAI.Application.Constants;

/// <summary>
/// Vocabulario inicial del catálogo (RF-09).
///
/// Estos valores se ofrecen desde el primer día, antes de que exista ninguna
/// entrada. Sin ellos, el primer perito que registra un tatuaje se encuentra con
/// un campo vacío y escribe lo primero que se le ocurre; el segundo escribe otra
/// cosa para lo mismo, y la clasificación deja de servir para agrupar.
///
/// No es una lista cerrada: quien registre un tatuaje puede escribir un valor
/// nuevo, y a partir de ahí ese valor se ofrece junto con estos.
/// </summary>
public static class CatalogVocabulary
{
    /// <summary>Familia iconográfica del tatuaje.</summary>
    public static readonly IReadOnlyList<string> Categorias =
    [
        "Letras o iniciales",
        "Número",
        "Frase o texto",
        "Símbolo",
        "Corona",
        "Estrella",
        "Animal",
        "Calavera",
        "Rostro o retrato",
        "Religioso",
        "Payaso",
        "Arma",
        "Naipe o dado",
        "Dinero",
        "Tribal",
        "Escudo o logotipo",
        "Otro"
    ];

    /// <summary>
    /// Zona del cuerpo. Se distingue izquierda y derecha donde es habitual, porque
    /// en un expediente "antebrazo" a secas obliga a volver a mirar la fotografía.
    /// </summary>
    public static readonly IReadOnlyList<string> UbicacionesCorporales =
    [
        "Antebrazo derecho",
        "Antebrazo izquierdo",
        "Brazo derecho",
        "Brazo izquierdo",
        "Hombro derecho",
        "Hombro izquierdo",
        "Mano derecha",
        "Mano izquierda",
        "Dedos",
        "Cuello",
        "Nuca",
        "Pecho",
        "Abdomen",
        "Espalda",
        "Costado",
        "Pierna derecha",
        "Pierna izquierda",
        "Pie o tobillo",
        "Rostro",
        "Cabeza"
    ];
}

namespace RTattooAI.Application.Exceptions;

/// <summary>
/// El servicio de inferencia ya tiene vectores con ese código de catálogo. Quien
/// enrola elige otro: juntar dos tatuajes bajo un mismo código hace que el catálogo
/// atribuya a uno la banda del otro.
/// </summary>
public class CatalogIdTakenException : Exception
{
    public CatalogIdTakenException(string catalogId)
        : base($"El código {catalogId} ya tiene vectores en la galería.")
    {
        CatalogId = catalogId;
    }

    public string CatalogId { get; }
}

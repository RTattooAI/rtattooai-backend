using RTattooAI.Domain.Enums;

namespace RTattooAI.Domain.Entities;

public class Consulta
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public required string Codigo { get; set; }
    /// <summary>
    /// Portada del análisis: copia denormalizada de la primera imagen (<see cref="Imagenes"/>
    /// con Orden = 0). Se conserva para que el historial, la miniatura y los reportes no
    /// tengan que cargar la colección completa.
    /// </summary>
    public required string ImagenOrigUrl { get; set; }
    public string? ImagenMiniaturaUrl { get; set; }
    public required string ImagenOrigHash { get; set; }
    public AnalysisStatus Estado { get; set; }
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    public string? ModelVersion { get; set; }
    public int? TiempoProcMs { get; set; }
    public DateTime FechaConsulta { get; set; } = DateTime.UtcNow;
    public DateTime? FechaFin { get; set; }
    public string? Notas { get; set; }

    /// <summary>
    /// Identificación opcional de la persona/detenido al que se le está analizando el
    /// tatuaje, para facilitar ubicar su expediente si es un caso antiguo o recurrente.
    /// </summary>
    public string? PersonaNombre { get; set; }
    public string? PersonaDocumento { get; set; }

    public Usuario? Usuario { get; set; }

    /// <summary>Fotografías que componen el análisis, en orden de carga.</summary>
    public ICollection<ConsultaImagen> Imagenes { get; set; } = [];

    /// <summary>Todas las detecciones del análisis, de todas sus imágenes.</summary>
    public ICollection<Deteccion> Detecciones { get; set; } = [];
}

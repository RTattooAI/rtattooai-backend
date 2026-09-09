using RTattooAI.Domain.Enums;

namespace RTattooAI.Domain.Entities;

/// <summary>
/// Una de las fotografías que componen un análisis. Un mismo caso suele necesitar
/// varias tomas (distintas zonas del cuerpo, distintos ángulos del mismo tatuaje),
/// y cada una se procesa por separado: tiene su propio estado, su propio tiempo de
/// inferencia y sus propias detecciones.
/// </summary>
public class ConsultaImagen
{
    public Guid Id { get; set; }
    public Guid ConsultaId { get; set; }

    /// <summary>Posición dentro del análisis, empezando en 0. La imagen 0 es la portada.</summary>
    public int Orden { get; set; }

    public required string ImagenUrl { get; set; }
    public string? ImagenMiniaturaUrl { get; set; }
    public required string ImagenHash { get; set; }

    public int? Width { get; set; }
    public int? Height { get; set; }

    /// <summary>Estado del procesamiento de esta imagen concreta, no del análisis completo.</summary>
    public AnalysisStatus Estado { get; set; }

    public string? ModelVersion { get; set; }
    public int? TiempoProcMs { get; set; }

    /// <summary>Procedencia de la toma: <c>archivo</c> (carga desde el equipo) o <c>camara</c>.</summary>
    public string Origen { get; set; } = OrigenImagen.Archivo;

    public Consulta? Consulta { get; set; }
    public ICollection<Deteccion> Detecciones { get; set; } = [];
}

public static class OrigenImagen
{
    public const string Archivo = "archivo";
    public const string Camara = "camara";

    public static readonly string[] Todos = [Archivo, Camara];
}

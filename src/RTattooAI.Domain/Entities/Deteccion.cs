using RTattooAI.Domain.Enums;

namespace RTattooAI.Domain.Entities;

public class Deteccion
{
    public Guid Id { get; set; }

    /// <summary>Análisis al que pertenece. Denormalizado: se deduce de la imagen, pero
    /// evita un join en las consultas del historial y de la bandeja de revisión.</summary>
    public Guid ConsultaId { get; set; }

    /// <summary>Fotografía concreta en la que se detectó este tatuaje.</summary>
    public Guid ConsultaImagenId { get; set; }
    public int BboxX { get; set; }
    public int BboxY { get; set; }
    public int BboxWidth { get; set; }
    public int BboxHeight { get; set; }
    public double Confianza { get; set; }
    public Verdict Verdict { get; set; }

    public EstadoValidacion EstadoValidacion { get; set; } = EstadoValidacion.Pendiente;
    public string? ObservacionesPerito { get; set; }
    public Guid? ValidadoPorId { get; set; }
    public DateTime? FechaValidacion { get; set; }

    public Consulta? Consulta { get; set; }
    public ConsultaImagen? ConsultaImagen { get; set; }
    public Usuario? ValidadoPor { get; set; }
    public ICollection<Coincidencia> Coincidencias { get; set; } = [];
}

namespace RTattooAI.Domain.Entities;

public class TatuajeCatalogo
{
    public Guid Id { get; set; }
    public required string Codigo { get; set; }
    public string? Nombre { get; set; }
    public string? Descripcion { get; set; }
    public Guid? BandaId { get; set; }
    public required string ImagenUrl { get; set; }
    public required string ImagenHash { get; set; }
    public required string Fuente { get; set; }
    public bool EstaActivo { get; set; } = true;
    public bool Validado { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public Guid RegistradoPorId { get; set; }

    /// <summary>
    /// Análisis (Consulta) del que proviene este tatuaje cuando se enroló desde una
    /// detección en vez de carga manual. Permite enlazar de vuelta al caso original
    /// cuando todavía no tiene banda confirmada.
    /// </summary>
    public Guid? ConsultaOrigenId { get; set; }

    public BandaCriminal? Banda { get; set; }
    public Usuario? RegistradoPor { get; set; }
    public Consulta? ConsultaOrigen { get; set; }
}

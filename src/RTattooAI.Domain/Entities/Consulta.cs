using RTattooAI.Domain.Enums;

namespace RTattooAI.Domain.Entities;

public class Consulta
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public required string Codigo { get; set; }
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

    public Usuario? Usuario { get; set; }
    public ICollection<Deteccion> Detecciones { get; set; } = [];
}

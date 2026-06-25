namespace RTattooAI.Domain.Entities;

public class BandaCriminal
{
    public Guid Id { get; set; }
    public required string Nombre { get; set; }
    public string? Alias { get; set; }
    public string? Region { get; set; }
    public string? Descripcion { get; set; }
    public string? NivelPeligro { get; set; }
    public bool EstaActivo { get; set; } = true;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public Guid? RegistradoPorId { get; set; }

    public Usuario? RegistradoPor { get; set; }
    public ICollection<TatuajeCatalogo> TatuajesCatalogo { get; set; } = [];
}

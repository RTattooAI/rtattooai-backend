namespace RTattooAI.Domain.Entities;

public class Usuario
{
    public Guid Id { get; set; }
    public required string NombreCompleto { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public Guid RolId { get; set; }
    public string? Dependencia { get; set; }
    public bool EstaActivo { get; set; } = true;
    public DateTime? UltimoAcceso { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaModificacion { get; set; }

    public Rol? Rol { get; set; }
    public ICollection<Consulta> Consultas { get; set; } = [];
    public ICollection<TatuajeCatalogo> TatuajesRegistrados { get; set; } = [];
}

namespace RTattooAI.Domain.Entities;

/// <summary>
/// Traza forense de las acciones sensibles del sistema (HU0014). Se escribe desde
/// un filtro global, de modo que ningún endpoint puede quedar sin registrar por
/// olvido. Es solo de escritura y lectura: nunca se actualiza ni se borra.
/// </summary>
public class RegistroAuditoria
{
    public Guid Id { get; set; }
    public Guid? UsuarioId { get; set; }
    public string? UsuarioEmail { get; set; }
    public string? UsuarioRol { get; set; }

    /// <summary>Acción en formato "METODO /ruta", p. ej. "POST /api/analyses".</summary>
    public required string Accion { get; set; }

    /// <summary>Identificador del recurso afectado cuando la ruta lo incluye.</summary>
    public string? Recurso { get; set; }

    public int StatusCode { get; set; }
    public string? DireccionIp { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public Usuario? Usuario { get; set; }
}

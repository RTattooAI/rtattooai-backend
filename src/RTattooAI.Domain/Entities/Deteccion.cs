using RTattooAI.Domain.Enums;

namespace RTattooAI.Domain.Entities;

public class Deteccion
{
    public Guid Id { get; set; }
    public Guid ConsultaId { get; set; }
    public int BboxX { get; set; }
    public int BboxY { get; set; }
    public int BboxWidth { get; set; }
    public int BboxHeight { get; set; }
    public double Confianza { get; set; }
    public Verdict Verdict { get; set; }

    public Consulta? Consulta { get; set; }
    public ICollection<Coincidencia> Coincidencias { get; set; } = [];
}

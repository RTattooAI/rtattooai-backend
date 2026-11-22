namespace RTattooAI.Domain.Entities;

/// <summary>
/// Versión del modelo de IA con la que se ha ejecutado el sistema (RF-16).
/// El identificador es la misma cadena que el servicio de inferencia devuelve en
/// <c>model_version</c> y que queda grabada en cada consulta, de modo que un
/// resultado siempre se puede atribuir a la versión que lo produjo.
/// </summary>
public class VersionModelo
{
    public Guid Id { get; set; }

    /// <summary>Cadena exacta reportada por el servicio de inferencia, p. ej. "yolov8m + dinov2-vits14 + head-v1".</summary>
    public required string Identificador { get; set; }

    public string? Detector { get; set; }
    public string? Embedder { get; set; }
    public string? Cabeza { get; set; }

    /// <summary>mAP@0.5 del detector medido sobre el conjunto de prueba.</summary>
    public double? MetricaMap50 { get; set; }

    /// <summary>Exactitud Top-1 de identificación sobre el conjunto de prueba.</summary>
    public double? MetricaTop1 { get; set; }

    /// <summary>Exactitud Top-5 de identificación sobre el conjunto de prueba.</summary>
    public double? MetricaTop5 { get; set; }

    /// <summary>Umbral operativo (rho) con el que se aceptó una coincidencia.</summary>
    public double? Umbral { get; set; }

    /// <summary>Margen (delta) exigido entre el primer y el segundo candidato.</summary>
    public double? Margen { get; set; }

    /// <summary>De dónde salen las métricas: informe, experimento, nota del autor.</summary>
    public string? Fuente { get; set; }

    public string? Notas { get; set; }

    /// <summary>Versión vigente en el despliegue. Solo una debería estarlo a la vez.</summary>
    public bool EsActiva { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}

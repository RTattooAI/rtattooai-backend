using RTattooAI.Application.Dtos.Dashboard;

namespace RTattooAI.Application.Abstractions;

public interface IDashboardService
{
    Task<DashboardMetricsDto> GetMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Métricas del panel en CSV (RF-17), en formato largo: una fila por métrica, con
    /// sección y clave. Así la serie diaria y las bandas más frecuentes caben en el
    /// mismo archivo que los totales sin inventar columnas vacías.
    /// </summary>
    Task<string> ExportCsvAsync(CancellationToken cancellationToken = default);
}

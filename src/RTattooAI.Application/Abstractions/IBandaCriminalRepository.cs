using RTattooAI.Domain.Entities;

namespace RTattooAI.Application.Abstractions;

public interface IBandaCriminalRepository
{
    /// <summary>
    /// Usada por el enrolamiento, que recibe el nombre de la banda como texto. Crea la
    /// banda si no existía, con los campos descriptivos vacíos para que supervisión los
    /// complete después desde el mantenimiento de bandas.
    /// </summary>
    Task<BandaCriminal> GetOrCreateByNombreAsync(string nombre, Guid registradoPorId, CancellationToken cancellationToken = default);

    Task<List<BandaCriminal>> GetAllAsync(bool incluirInactivas = true, CancellationToken cancellationToken = default);

    Task<BandaCriminal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<BandaCriminal?> GetByNombreAsync(string nombre, CancellationToken cancellationToken = default);

    /// <summary>Número de tatuajes del catálogo que apuntan a cada banda, por identificador.</summary>
    Task<Dictionary<Guid, int>> CountTatuajesPorBandaAsync(CancellationToken cancellationToken = default);

    Task AddAsync(BandaCriminal banda, CancellationToken cancellationToken = default);

    void Remove(BandaCriminal banda);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

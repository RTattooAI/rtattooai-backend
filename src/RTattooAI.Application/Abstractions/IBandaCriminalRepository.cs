using RTattooAI.Domain.Entities;

namespace RTattooAI.Application.Abstractions;

public interface IBandaCriminalRepository
{
    Task<BandaCriminal> GetOrCreateByNombreAsync(string nombre, Guid registradoPorId, CancellationToken cancellationToken = default);
}

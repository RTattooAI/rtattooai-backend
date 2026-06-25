using RTattooAI.Application.Dtos.Catalog;

namespace RTattooAI.Application.Abstractions;

public interface ICatalogService
{
    Task<CatalogItemDto> EnrollAsync(Guid usuarioId, Stream imageStream, string fileName, string contentType, long contentLength, string band, CancellationToken cancellationToken = default);

    Task<List<CatalogItemDto>> GetAllAsync(CancellationToken cancellationToken = default);
}

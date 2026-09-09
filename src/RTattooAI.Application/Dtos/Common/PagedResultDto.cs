namespace RTattooAI.Application.Dtos.Common;

/// <summary>
/// Página de resultados. Se devuelve el total para que el cliente pueda dibujar el
/// paginador sin tener que traerse el resto de las páginas.
/// </summary>
public record PagedResultDto<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

namespace AuditLog.Application.Results;

/// <summary>
/// Resultado paginado de uma query de leitura.
/// Imutável; transporta a página corrente, o tamanho da página e o total de registros.
/// </summary>
/// <typeparam name="T">Tipo dos itens retornados.</typeparam>
/// <param name="Items">Itens da página corrente.</param>
/// <param name="Page">Número da página (base 1).</param>
/// <param name="PageSize">Tamanho da página solicitado.</param>
/// <param name="TotalCount">Total de registros (antes da paginação).</param>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);

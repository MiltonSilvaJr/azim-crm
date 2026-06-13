using TenantAdministration.Domain.Aggregates;

namespace TenantAdministration.Application.Ports;

/// <summary>
/// Porta de saída para persistência do agregado <see cref="Tenant"/>.
/// Implementação concreta vem na Onda 4 (Infrastructure — EF Core).
/// </summary>
public interface ITenantRepository
{
    /// <summary>
    /// Verifica se um slug já está em uso (unicidade global — Req 2).
    /// </summary>
    /// <param name="slug">Valor do slug a verificar.</param>
    /// <param name="ct">Token de cancelamento.</param>
    ValueTask<bool> ExistsSlugAsync(string slug, CancellationToken ct = default);

    /// <summary>
    /// Busca o agregado <see cref="Tenant"/> pelo identificador único.
    /// Retorna <c>null</c> se não encontrado.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant.</param>
    /// <param name="ct">Token de cancelamento.</param>
    Task<Tenant?> FindByIdAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Busca o agregado <see cref="Tenant"/> pelo slug.
    /// Retorna <c>null</c> se não encontrado.
    /// </summary>
    /// <param name="slug">Slug do tenant.</param>
    /// <param name="ct">Token de cancelamento.</param>
    Task<Tenant?> FindBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>
    /// Adiciona um novo <see cref="Tenant"/> ao repositório.
    /// A persistência é confirmada pela transação do <c>TransactionBehavior</c>.
    /// </summary>
    /// <param name="tenant">Agregado a persistir.</param>
    /// <param name="ct">Token de cancelamento.</param>
    Task AddAsync(Tenant tenant, CancellationToken ct = default);

    /// <summary>
    /// Atualiza o estado de um <see cref="Tenant"/> existente.
    /// A persistência é confirmada pela transação do <c>TransactionBehavior</c>.
    /// </summary>
    /// <param name="tenant">Agregado atualizado.</param>
    /// <param name="ct">Token de cancelamento.</param>
    Task UpdateAsync(Tenant tenant, CancellationToken ct = default);
}

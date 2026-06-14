using Microsoft.EntityFrameworkCore;
using PartnerManagement.Infrastructure.Persistence;

namespace PartnerManagement.Infrastructure.Idempotency;

/// <summary>
/// Repositório de chaves de idempotência para operações do <c>data-migration</c>.
/// Persiste em <c>idempotency_keys</c> na mesma transação da escrita de domínio.
/// Mapeia: design §6.5, PM-ERR-010, TASK-20.
/// </summary>
public sealed class IdempotencyKeyRepository
{
    private readonly PartnerManagementDbContext _dbContext;

    public IdempotencyKeyRepository(PartnerManagementDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Verifica se a chave de idempotência já existe para o tenant.
    /// </summary>
    /// <param name="tenantId">Tenant da operação.</param>
    /// <param name="key">Chave de idempotência.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Entrada existente ou <c>null</c> se não encontrada.</returns>
    public async Task<IdempotencyKey?> FindAsync(
        Guid tenantId,
        string key,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.IdempotencyKeys
            .IgnoreQueryFilters() // busca direta por chave composta — filtro global usa TenantId
            .FirstOrDefaultAsync(k => k.TenantId == tenantId && k.Key == key, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Persiste uma nova chave de idempotência.
    /// </summary>
    /// <param name="entry">Entrada a ser persistida.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    public async Task AddAsync(IdempotencyKey entry, CancellationToken cancellationToken = default)
    {
        await _dbContext.IdempotencyKeys.AddAsync(entry, cancellationToken).ConfigureAwait(false);
    }
}

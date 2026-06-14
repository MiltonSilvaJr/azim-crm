using AccountManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AccountManagement.Infrastructure.ReadPorts;

/// <summary>
/// Exceção lançada quando uma <c>Idempotency-Key</c> é reutilizada com payload divergente.
/// Mapeada para ACC-ERR-009 na camada de API.
///
/// Mapeia: design §6.5, design §12 (ACC-ERR-009), TASK-12.
/// </summary>
public sealed class IdempotencyKeyConflictException : Exception
{
    /// <summary>Identificador do recurso criado pela operação original.</summary>
    public Guid? OriginalResponseRef { get; }

    /// <summary>
    /// Inicializa com a referência da resposta original.
    /// </summary>
    public IdempotencyKeyConflictException(Guid? originalResponseRef)
        : base("Requisição duplicada com payload divergente (ACC-ERR-009).")
    {
        OriginalResponseRef = originalResponseRef;
    }
}

/// <summary>
/// Repositório para gerenciamento de chaves de idempotência.
///
/// Deduplicação de escritas: chave nova → registra e retorna <c>null</c>;
/// mesma chave + payload igual → retorna referência original;
/// mesma chave + payload divergente → lança <see cref="IdempotencyKeyConflictException"/> (ACC-ERR-009).
///
/// Mapeia: design §6.5, design §7 (idempotency_keys), TASK-12 (ST-03).
/// </summary>
public sealed class IdempotencyKeyRepository
{
    private readonly AccountManagementDbContext _context;

    public IdempotencyKeyRepository(AccountManagementDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Verifica e registra uma chave de idempotência.
    /// </summary>
    /// <param name="tenantId">Tenant da operação.</param>
    /// <param name="idempotencyKey">Chave de idempotência fornecida pelo cliente.</param>
    /// <param name="requestHash">Hash do payload da requisição atual.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// <c>null</c> quando a chave é nova (operação deve prosseguir);
    /// <see cref="Guid"/> quando a chave já existe com payload idêntico (resposta anterior).
    /// </returns>
    /// <exception cref="IdempotencyKeyConflictException">
    /// Lançada quando a chave já existe com payload divergente (ACC-ERR-009).
    /// </exception>
    public async Task<Guid?> CheckAndRegisterAsync(
        Guid tenantId,
        string idempotencyKey,
        string requestHash,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.IdempotencyKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(
                k => k.TenantId == tenantId && k.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (existing is null)
        {
            // Chave nova: registra sem responseRef (será atualizado após criação)
            _context.IdempotencyKeys.Add(new IdempotencyKeyEntry
            {
                TenantId = tenantId,
                IdempotencyKey = idempotencyKey,
                RequestHash = requestHash,
                ResponseRef = null,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);
            return null;
        }

        // Chave existente: verifica hash do payload
        if (existing.RequestHash != requestHash)
            throw new IdempotencyKeyConflictException(existing.ResponseRef);

        // Mesmo payload: retorna referência original
        return existing.ResponseRef;
    }

    /// <summary>
    /// Atualiza a referência de resposta de uma chave de idempotência após a criação do recurso.
    /// </summary>
    public async Task UpdateResponseRefAsync(
        Guid tenantId,
        string idempotencyKey,
        Guid responseRef,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.IdempotencyKeys
            .FirstOrDefaultAsync(
                k => k.TenantId == tenantId && k.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (existing is not null)
        {
            // EF Core rastreia a entidade; substituímos via nova entrada
            _context.IdempotencyKeys.Remove(existing);
            _context.IdempotencyKeys.Add(new IdempotencyKeyEntry
            {
                TenantId = existing.TenantId,
                IdempotencyKey = existing.IdempotencyKey,
                RequestHash = existing.RequestHash,
                ResponseRef = responseRef,
                CreatedAt = existing.CreatedAt
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

namespace AccountManagement.Infrastructure.Persistence;

/// <summary>
/// Entidade de persistência para a tabela <c>idempotency_keys</c>.
///
/// PK composta: (<see cref="TenantId"/>, <see cref="IdempotencyKey"/>).
/// Usada pelo <c>IdempotencyKeyRepository</c> para deduplicação de escritas (design §6.5).
///
/// Mapeia: design §7 (idempotency_keys), design §6.5, TASK-09, TASK-12.
/// </summary>
internal sealed class IdempotencyKeyEntry
{
    /// <summary>Tenant da operação idempotente (parte da PK composta).</summary>
    public Guid TenantId { get; init; }

    /// <summary>Chave de idempotência fornecida pelo cliente (parte da PK composta).</summary>
    public string IdempotencyKey { get; init; } = string.Empty;

    /// <summary>
    /// Hash do payload da requisição original.
    /// Detecta payload divergente na mesma chave (ACC-ERR-009).
    /// </summary>
    public string RequestHash { get; init; } = string.Empty;

    /// <summary>
    /// Referência ao recurso criado pela operação original (ex.: AccountId ou ContactId).
    /// <c>null</c> quando a operação não produziu recurso identificável.
    /// </summary>
    public Guid? ResponseRef { get; init; }

    /// <summary>Momento de criação do registro (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

namespace PartnerManagement.Infrastructure.Idempotency;

/// <summary>
/// Entidade de persistência de chave de idempotência para operações do <c>data-migration</c>.
/// Armazenada em <c>idempotency_keys</c> com chave primária composta (tenant_id + idempotency_key).
/// Permite deduplicação de reentregas no import transacional (design §6.5, TASK-20).
/// Mapeia: design §6.5, PM-ERR-010.
/// </summary>
public sealed class IdempotencyKey
{
    /// <summary>Tenant ao qual a chave pertence (RLS, DD-001).</summary>
    public Guid TenantId { get; private init; }

    /// <summary>Chave de idempotência fornecida pelo cliente (header <c>Idempotency-Key</c>).</summary>
    public string Key { get; private init; } = null!;

    /// <summary>Hash SHA-256 do payload da requisição original para detecção de divergência.</summary>
    public string RequestHash { get; private init; } = null!;

    /// <summary>Referência ao recurso criado (ex.: <c>partner_id</c>). Pode ser <c>null</c> se ainda em processamento.</summary>
    public Guid? ResponseRef { get; private set; }

    /// <summary>Instante de criação da chave (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private init; }

    // EF Core: construtor privado para rehidratação
    private IdempotencyKey()
    {
    }

    /// <summary>
    /// Cria uma nova entrada de idempotência.
    /// </summary>
    /// <param name="tenantId">Tenant da operação.</param>
    /// <param name="key">Chave de idempotência.</param>
    /// <param name="requestHash">Hash do payload da requisição.</param>
    /// <param name="createdAt">Instante atual (UTC).</param>
    public static IdempotencyKey Create(
        Guid tenantId,
        string key,
        string requestHash,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);

        return new IdempotencyKey
        {
            TenantId = tenantId,
            Key = key,
            RequestHash = requestHash,
            CreatedAt = createdAt
        };
    }

    /// <summary>Associa o recurso criado à chave de idempotência após processamento bem-sucedido.</summary>
    /// <param name="responseRef">Identificador do recurso criado.</param>
    public void SetResponseRef(Guid responseRef)
    {
        ResponseRef = responseRef;
    }
}

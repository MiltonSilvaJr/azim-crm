namespace ActivityManagement.Infrastructure.Tokens;

/// <summary>
/// Entidade de dados do token de ação do digest.
/// Owned pelo digest (BC-06); activity-management lê e consome (DD-003, ADR-0006).
/// O schema é criado exclusivamente pelo digest — esta entidade está mapeada com
/// <c>ExcludeFromMigrations</c> para que o activity-management jamais emita DDL na tabela.
/// Armazena apenas o <see cref="TokenHash"/> (SHA-256, 32 bytes BYTEA) —
/// o token em claro só transita no link e nunca é persistido (RNF 5, DD-007).
/// <see cref="ActivityId"/> é NOT NULL (referência lógica sem FK física — DD-001).
/// RLS obrigatória: políticas <c>rls_digest_action_tokens_tenant</c> comparam
/// <c>tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid</c> (ADR-0001).
/// Mapeia: design §6.4, §7, DD-003, DD-007, ADR-0006, TASK-13.
/// </summary>
public sealed class DigestActionToken
{
    /// <summary>Identificador do registro (UUID, PK).</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Tenant ao qual o token pertence (obrigatório).</summary>
    public Guid TenantId { get; init; }

    /// <summary>Usuário para quem o token foi emitido.</summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Atividade referenciada pelo token.
    /// NOT NULL — referência lógica sem FK física (DD-001, ADR-0006).
    /// </summary>
    public Guid ActivityId { get; init; }

    /// <summary>
    /// Ação do token: <c>Complete</c> ou <c>Reschedule</c>.
    /// Casing canônico do enum <c>ActionType</c> do digest (BC-06).
    /// </summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>
    /// Hash SHA-256 do token opaco (32 bytes, BYTEA no PostgreSQL).
    /// O token em claro nunca é persistido (DD-007, RNF 5).
    /// Índice único <c>uq_digest_action_tokens_hash</c> garante unicidade.
    /// </summary>
    public byte[] TokenHash { get; init; } = [];

    /// <summary>Instante de expiração do token (TTL configurável, RNF 5.4).</summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Instante em que o token foi consumido.
    /// Nulo enquanto disponível para uso; preenchido pelo <c>DigestActionTokenAdapter</c>
    /// na mesma transação da conclusão/reagendamento (DD-003, RNF 5.2).
    /// </summary>
    public DateTimeOffset? UsedAt { get; set; }

    /// <summary>Instante de criação do token.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

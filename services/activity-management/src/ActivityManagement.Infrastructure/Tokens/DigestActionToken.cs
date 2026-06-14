namespace ActivityManagement.Infrastructure.Tokens;

/// <summary>
/// Entidade de dados do token de ação do digest.
/// Owned pelo digest (BC-06); activity-management lê e consome (DD-003).
/// Armazena apenas o <see cref="TokenHash"/> — o token em claro só transita no link
/// e nunca é persistido (RNF 5, design §7).
/// RLS obrigatória: políticas <c>rls_digest_action_tokens_tenant</c> comparam
/// <c>tenant_id = current_setting('app.current_tenant')::uuid</c> (ADR-0001).
/// Mapeia: design §6.4, §7, DD-003, TASK-13.
/// </summary>
public sealed class DigestActionToken
{
    /// <summary>Identificador do registro (UUID, PK).</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Tenant ao qual o token pertence (obrigatório).</summary>
    public Guid TenantId { get; init; }

    /// <summary>Usuário para quem o token foi emitido.</summary>
    public Guid UserId { get; init; }

    /// <summary>Atividade referenciada pelo token (nullable — link pode ser órfão).</summary>
    public Guid? ActivityId { get; init; }

    /// <summary>Ação do token: <c>complete</c> ou <c>reschedule</c>.</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>
    /// Hash do token opaco (SHA-256 ou equivalente).
    /// O token em claro nunca é persistido (DD-003, RNF 5).
    /// Índice único <c>uq_digest_action_tokens_hash</c> garante unicidade.
    /// </summary>
    public string TokenHash { get; init; } = string.Empty;

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

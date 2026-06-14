namespace ActivityManagement.Infrastructure.Audit;

/// <summary>
/// Registro de auditoria imutável (append-only).
/// Gravado em <c>audit_logs</c> com trigger de imutabilidade e
/// <c>REVOKE UPDATE, DELETE, TRUNCATE</c> (RNF 2, design §7).
/// O <see cref="DeltaJson"/> não contém <c>title</c> ou <c>description</c> em claro (RNF 7.2).
/// Mapeia: design §6.6, §7, RNF 2, TASK-13.
/// </summary>
public sealed class AuditLog
{
    /// <summary>Identificador do registro (UUID, PK).</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Tenant da operação (obrigatório).</summary>
    public Guid TenantId { get; init; }

    /// <summary>Autor da operação; nulo quando a ação é via token do digest.</summary>
    public Guid? UserId { get; init; }

    /// <summary>Tipo da entidade auditada (ex: <c>"Activity"</c>).</summary>
    public string EntityType { get; init; } = string.Empty;

    /// <summary>Identificador da entidade auditada.</summary>
    public Guid EntityId { get; init; }

    /// <summary>
    /// Ação realizada: <c>created</c>, <c>updated</c>, <c>completed</c>,
    /// <c>cancelled</c>, <c>rescheduled</c>, <c>deleted</c>.
    /// </summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>
    /// Snapshot das mudanças em JSON mascarado pelo <see cref="PiiMasker"/>.
    /// Nunca contém <c>title</c> ou <c>description</c> em claro (RNF 7.2).
    /// </summary>
    public string DeltaJson { get; init; } = string.Empty;

    /// <summary>
    /// Identificador de correlação da operação.
    /// Obrigatório na conclusão via token do digest (RNF 2.4, Req 7.8).
    /// </summary>
    public Guid? CorrelationId { get; init; }

    /// <summary>Instante do registro de auditoria (imutável após inserção).</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

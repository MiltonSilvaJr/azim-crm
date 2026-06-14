namespace AccountManagement.Infrastructure.Persistence;

/// <summary>
/// Entidade de persistência para a tabela <c>audit_logs</c> (append-only).
///
/// Esta classe é usada apenas no Infrastructure para persistência do log imutável.
/// Não é um domain event — é o registro gravado na tabela de auditoria.
///
/// A tabela é protegida contra UPDATE/DELETE por trigger PL/pgSQL e REVOKE (RNF 8, DD-007).
/// Não há coluna <c>updated_at</c> — append-only por definição (design §7).
///
/// Mapeia: design §7 (audit_logs), RNF 8, TASK-09.
/// </summary>
internal sealed class AuditLogEntry
{
    /// <summary>Identificador único do log de auditoria (UUID).</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Tenant do evento auditado.</summary>
    public Guid TenantId { get; init; }

    /// <summary>Usuário que executou a ação.</summary>
    public Guid UserId { get; init; }

    /// <summary>Tipo de entidade auditada (ex.: <c>Account</c>, <c>Contact</c>).</summary>
    public string EntityType { get; init; } = string.Empty;

    /// <summary>Identificador da entidade auditada.</summary>
    public Guid EntityId { get; init; }

    /// <summary>Ação executada (ex.: <c>created</c>, <c>updated</c>, <c>forgotten</c>).</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>
    /// Delta em JSON com PII mascarada pelo <c>PiiMasker</c> (DD-003, Req 8.4).
    /// Nunca contém PII em texto claro.
    /// </summary>
    public string DeltaJson { get; init; } = string.Empty;

    /// <summary>Momento de criação do registro de auditoria (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

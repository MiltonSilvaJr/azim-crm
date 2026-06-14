namespace ActivityManagement.Application.Ports;

/// <summary>
/// Entrada de auditoria gravada em <c>audit_logs</c> (append-only — RNF 2).
/// O <c>DeltaJson</c> não deve conter <c>title</c> ou <c>description</c> em claro (RNF 7.2).
/// Mapeia: design §6.6, RNF 2, DD-009.
/// </summary>
/// <param name="TenantId">Tenant da operação.</param>
/// <param name="UserId">Autor da operação; nulo quando ação via token do digest.</param>
/// <param name="EntityType">Tipo da entidade auditada (ex: <c>"Activity"</c>).</param>
/// <param name="EntityId">Identificador da entidade auditada.</param>
/// <param name="Action">Ação realizada: <c>created</c>, <c>updated</c>, <c>completed</c>,
/// <c>cancelled</c>, <c>rescheduled</c>, <c>deleted</c>.</param>
/// <param name="DeltaJson">Snapshot das mudanças em JSON (sem PII — RNF 7.2).</param>
/// <param name="CorrelationId">Correlação da operação; obrigatório na conclusão via token (RNF 2.4).</param>
public sealed record AuditEntry(
    Guid    TenantId,
    Guid?   UserId,
    string  EntityType,
    Guid    EntityId,
    string  Action,
    string  DeltaJson,
    Guid?   CorrelationId = null);

/// <summary>
/// Port de publicação de entradas de auditoria imutáveis (RNF 2, ADR-0003).
/// Implementado em Infrastructure por <c>AuditPublisher</c> que grava em <c>audit_logs</c>
/// append-only com trigger de imutabilidade e <c>REVOKE UPDATE/DELETE/TRUNCATE</c>.
/// Mapeia: design §6.6, Req 2, Req 7.8, TASK-07.
/// </summary>
public interface IAuditPublisher
{
    /// <summary>
    /// Registra uma entrada de auditoria na mesma transação corrente.
    /// </summary>
    /// <param name="entry">Dados da entrada de auditoria.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task PublishAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}

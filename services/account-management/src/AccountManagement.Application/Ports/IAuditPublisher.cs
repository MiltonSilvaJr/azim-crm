using AccountManagement.Domain.Shared;

namespace AccountManagement.Application.Ports;

/// <summary>
/// Porta de saída para publicação de eventos de auditoria imutáveis.
///
/// A implementação concreta na Infrastructure grava em <c>audit_logs</c> (append-only)
/// com PII mascarada pelo <c>PiiMasker</c> (DD-003, RNF 8, Req 8).
///
/// Declarada na Application; implementada na Infrastructure (design §3).
///
/// Mapeia: design §5 (Ports), design §6.6, Req 8, RNF 8, DD-003, DD-007.
/// </summary>
public interface IAuditPublisher
{
    /// <summary>
    /// Publica um evento de domínio no log de auditoria.
    /// O <c>delta_json</c> é mascarado pelo <c>PiiMasker</c> antes de persistir (DD-003).
    /// </summary>
    /// <param name="domainEvent">Evento a auditar.</param>
    /// <param name="actorId">Identificador do usuário que executou a ação.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task PublishAsync(
        IDomainEvent domainEvent,
        Guid actorId,
        CancellationToken cancellationToken = default);
}

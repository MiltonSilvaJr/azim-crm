namespace OpportunityPipeline.Domain.Opportunities.Ports;

/// <summary>
/// Porta de publicação de auditoria imutável (RNF 6).
/// Implementação grava em audit_logs via Outbox na mesma transação.
/// Mapeia: RNF 6, design §6.6.
/// </summary>
public interface IAuditPublisher
{
    /// <summary>
    /// Publica um registro de auditoria com delta mascarado de PII.
    /// </summary>
    Task PublishAsync(
        Guid tenantId,
        Guid aggregateId,
        string aggregateType,
        string action,
        Guid actorId,
        object? delta,
        CancellationToken cancellationToken = default);
}

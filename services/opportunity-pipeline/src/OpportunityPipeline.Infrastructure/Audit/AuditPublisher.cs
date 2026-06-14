using Microsoft.Extensions.Logging;
using OpportunityPipeline.Domain.Opportunities.Ports;
using OpportunityPipeline.Infrastructure.Outbox;
using OpportunityPipeline.Infrastructure.Persistence;
using System.Text.Json;

namespace OpportunityPipeline.Infrastructure.Audit;

/// <summary>
/// Implementação de IAuditPublisher que grava em audit_logs (append-only) via Outbox.
/// O registro é feito na MESMA transação do estado de negócio (RNF 6, RNF 6.3).
/// PII mascarado pelo PiiMasker antes da persistência.
/// Mapeia: RNF 6, design §6.6, TASK-16.
/// </summary>
public sealed class AuditPublisher(
    OpportunityDbContext context,
    ILogger<AuditPublisher> logger)
    : IAuditPublisher
{
    private const string AuditEventType = "audit.log.v1";

    /// <inheritdoc/>
    public async Task PublishAsync(
        Guid tenantId,
        Guid aggregateId,
        string aggregateType,
        string action,
        Guid actorId,
        object? delta,
        CancellationToken cancellationToken = default)
    {
        // Mascara PII antes de persistir (RNF 6.3)
        var maskedDelta = PiiMasker.MaskAndSerialize(delta);

        var auditPayload = new
        {
            aggregate_id = aggregateId,
            aggregate_type = aggregateType,
            action,
            actor_id = actorId,
            occurred_at = DateTimeOffset.UtcNow,
            delta = maskedDelta
        };

        // Grava no Outbox — mesma transação (append-only)
        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventType = AuditEventType,
            Payload = JsonSerializer.Serialize(auditPayload),
            Status = "pending",
            CreatedAt = DateTimeOffset.UtcNow,
            Attempts = 0
        };

        await context.OutboxMessages.AddAsync(outboxMessage, cancellationToken)
            .ConfigureAwait(false);

        // Não logar delta (pode conter dados sensíveis mesmo após mascaramento)
        logger.LogDebug(
            "AuditPublisher: registro de auditoria enfileirado para {AggregateType}:{AggregateId}, ação={Action}.",
            aggregateType,
            aggregateId,
            action);
    }
}

namespace ActivityManagement.Infrastructure.Outbox;

using System.Text.Json;
using ActivityManagement.Application.Ports;
using ActivityManagement.Domain.Activities.Events;
using ActivityManagement.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="IOutboxPublisher"/> que persiste o evento no Outbox
/// na mesma transação EF Core da escrita de negócio (design §6.5, DD-007).
/// A publicação efetiva ao Cloud Pub/Sub é responsabilidade do <see cref="OutboxRelayWorker"/>.
/// O <c>PayloadJson</c> nunca contém <c>title</c> ou <c>description</c> em claro (RNF 7.2).
/// Mapeia: TASK-16, DD-007, Req 14.
/// </summary>
internal sealed class OutboxPublisher : IOutboxPublisher
{
    private readonly ActivityManagementDbContext _db;

    /// <summary>Inicializa o publisher com o contexto EF corrente.</summary>
    public OutboxPublisher(ActivityManagementDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(
        DomainEvent       domainEvent,
        string?           deduplicationKey  = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var payloadJson = JsonSerializer.Serialize(domainEvent, domainEvent.GetType());

        // Extrai TenantId do evento concreto via propriedade convencional (todos os eventos do módulo
        // carregam TenantId como propriedade pública — DomainEvent base não o declara para evitar
        // dependência desnecessária na base, mas todos os concretos o têm).
        var tenantIdProp = domainEvent.GetType().GetProperty("TenantId");
        var tenantId = tenantIdProp?.GetValue(domainEvent) is Guid tid ? tid : Guid.Empty;

        var message = new OutboxMessage
        {
            TenantId    = tenantId,
            EventType   = domainEvent.GetType().Name,
            DedupKey    = deduplicationKey,
            PayloadJson = payloadJson,
            OccurredAt  = domainEvent.OccurredAt,
        };

        _db.OutboxMessages.Add(message);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sobrecarga interna usada pelos testes de integração para enfileirar sem instância de DomainEvent.
    /// Não exposta via interface — usada apenas em testes.
    /// </summary>
    internal Task EnqueueAsync(
        string            eventType,
        string            payloadJson,
        Guid              tenantId,
        string?           deduplicationKey  = null,
        CancellationToken cancellationToken = default)
    {
        var message = new OutboxMessage
        {
            TenantId    = tenantId,
            EventType   = eventType,
            DedupKey    = deduplicationKey,
            PayloadJson = payloadJson,
            OccurredAt  = DateTimeOffset.UtcNow,
        };

        _db.OutboxMessages.Add(message);
        return Task.CompletedTask;
    }
}

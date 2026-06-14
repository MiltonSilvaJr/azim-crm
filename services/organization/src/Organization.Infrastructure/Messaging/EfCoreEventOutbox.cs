using System.Text.Json;
using Organization.Application.Ports;
using Organization.Domain.Events;
using Organization.Infrastructure.Persistence;

namespace Organization.Infrastructure.Messaging;

/// <summary>
/// Implementação de <see cref="IEventOutbox"/> via EF Core.
/// Insere eventos na tabela <c>outbox_events</c> dentro da transação corrente do handler,
/// garantindo atomicidade estado↔evento (DD-004, §6.6).
/// </summary>
public sealed class EfCoreEventOutbox : IEventOutbox
{
    private readonly OrganizationDbContext _context;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary>Inicializa o outbox com o contexto de banco.</summary>
    public EfCoreEventOutbox(OrganizationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(
        IDomainEvent domainEvent,
        Guid tenantId,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        // Serializa o evento; o tipo concreto é registrado para desserialização pelo worker.
        var payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions);
        var eventType = domainEvent.GetType().Name;

        var outboxEvent = new OutboxEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventType = eventType,
            Payload = payload,
            CorrelationId = correlationId,
            OccurredAt = domainEvent.OccurredAt,
        };

        _context.OutboxEvents.Add(outboxEvent);

        return Task.CompletedTask;
    }
}

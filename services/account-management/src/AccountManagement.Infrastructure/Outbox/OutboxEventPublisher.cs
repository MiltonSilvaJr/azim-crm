using System.Text.Json;
using AccountManagement.Application.Ports;
using AccountManagement.Domain.Shared;
using AccountManagement.Infrastructure.Audit;
using AccountManagement.Infrastructure.Persistence;

namespace AccountManagement.Infrastructure.Outbox;

/// <summary>
/// Implementação de <see cref="IEventPublisher"/> que grava domain events na tabela
/// <c>outbox_messages</c> na mesma transação da escrita de domínio (DD-007).
///
/// O payload é mascarado pelo <see cref="PiiMasker"/> antes de gravar (DD-003, RNF 1.2).
/// O relay assíncrono (<see cref="OutboxRelayWorker"/>) publica no Pub/Sub posteriormente.
///
/// Mapeia: design §6.6, IEventPublisher, DD-007, TASK-11.
/// </summary>
internal sealed class OutboxEventPublisher : IEventPublisher
{
    private readonly AccountManagementDbContext _context;
    private readonly PiiMasker _piiMasker;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    public OutboxEventPublisher(AccountManagementDbContext context, PiiMasker piiMasker)
    {
        _context = context;
        _piiMasker = piiMasker;
    }

    /// <inheritdoc />
    public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var eventType = GetEventType(domainEvent);
        var tenantId = GetTenantId(domainEvent);
        var rawPayload = SerializeEvent(domainEvent);

        // Mascara PII no payload antes de gravar no Outbox (DD-003, RNF 1.2)
        var maskedPayload = _piiMasker.MaskContactDelta(rawPayload);

        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventType = eventType,
            PayloadJson = maskedPayload,
            OccurredAt = domainEvent.OccurredAt,
            PublishedAt = null
        };

        _context.OutboxMessages.Add(message);
        // SaveChanges é chamado pelo TransactionBehavior no commit — não chamamos aqui
        // para manter a atomicidade da transação (DD-007)
        await Task.CompletedTask;
    }

    // =========================================================================
    // Helpers privados
    // =========================================================================

    private static string GetEventType(IDomainEvent domainEvent) => domainEvent switch
    {
        Domain.Accounts.Events.AccountCreated => "account.created.v1",
        Domain.Accounts.Events.AccountUpdated => "account.updated.v1",
        Domain.Accounts.Events.ContactLinked => "account.contact_linked.v1",
        Domain.Accounts.Events.ContactForgotten => "account.contact_forgotten.v1",
        _ => $"unknown.{domainEvent.GetType().Name.ToLowerInvariant()}.v1"
    };

    private static Guid GetTenantId(IDomainEvent domainEvent) => domainEvent switch
    {
        Domain.Accounts.Events.AccountCreated e => e.TenantId,
        Domain.Accounts.Events.AccountUpdated e => e.TenantId,
        Domain.Accounts.Events.ContactLinked e => e.TenantId,
        Domain.Accounts.Events.ContactForgotten e => e.TenantId,
        _ => Guid.Empty
    };

    private static string SerializeEvent(IDomainEvent domainEvent)
    {
        // Serializa com envelope padrão (design §9)
        var envelope = new
        {
            event_id = domainEvent.EventId,
            event_type = GetEventType(domainEvent),
            event_version = "v1",
            occurred_at = domainEvent.OccurredAt,
            payload = domainEvent
        };
        return JsonSerializer.Serialize(envelope, JsonOptions);
    }
}

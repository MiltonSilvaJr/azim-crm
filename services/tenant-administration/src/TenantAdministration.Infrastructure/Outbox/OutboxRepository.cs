using System.Text.Json;
using TenantAdministration.Application.Ports;
using TenantAdministration.Domain.Events;
using TenantAdministration.Infrastructure.Persistence;

namespace TenantAdministration.Infrastructure.Outbox;

/// <summary>
/// Implementação de <see cref="IEventOutbox"/> que persiste eventos na tabela
/// <c>outbox_events</c> dentro da transação corrente (design.md §6.6, Req 11.4).
/// Mapeia domain events para o envelope TRD (design.md §9, TRD §9.2).
/// </summary>
public sealed class OutboxRepository : IEventOutbox
{
    private readonly TenantAdministrationDbContext _db;
    private readonly ITenantContext _tenantContext;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <param name="db">DbContext do módulo.</param>
    /// <param name="tenantContext">Contexto do tenant corrente (para envelope TRD).</param>
    public OutboxRepository(TenantAdministrationDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc/>
    public async Task AppendAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        var outboxEvent = MapToOutboxEvent(domainEvent);
        await _db.OutboxEvents.AddAsync(outboxEvent, ct);
    }

    /// <inheritdoc/>
    public async Task AppendRangeAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            var outboxEvent = MapToOutboxEvent(domainEvent);
            await _db.OutboxEvents.AddAsync(outboxEvent, ct);
        }
    }

    private OutboxEvent MapToOutboxEvent(IDomainEvent domainEvent)
    {
        var (eventType, aggregateId, tenantId, payload) = domainEvent switch
        {
            TenantProvisioned e => (
                "tenant.provisioned.v1",
                e.TenantId,
                (Guid?)e.TenantId,
                SerializePayload(new
                {
                    e.TenantId,
                    e.Slug,
                    e.DisplayName,
                    e.Timezone,
                    e.DigestTime,
                    e.ProvisionedAt
                    // adminEmail NUNCA incluído no payload — LGPD (design.md §10)
                })),
            TenantSuspended e => (
                "tenant.suspended.v1",
                e.TenantId,
                (Guid?)e.TenantId,
                SerializePayload(new { e.TenantId, e.Slug, OccurredAt = e.SuspendedAt })),
            TenantReactivated e => (
                "tenant.reactivated.v1",
                e.TenantId,
                (Guid?)e.TenantId,
                SerializePayload(new { e.TenantId, e.Slug, OccurredAt = e.ReactivatedAt })),
            BrandingChanged e => (
                "tenant.branding_changed.v1",
                e.TenantId,
                (Guid?)e.TenantId,
                SerializePayload(new { e.TenantId, e.Slug, e.WcagContrastOk, e.ChangedAt })),
            DigestConfigChanged e => (
                "tenant.digest_config_changed.v1",
                e.TenantId,
                (Guid?)e.TenantId,
                SerializePayload(new { e.TenantId, e.Timezone, e.DigestTime, OccurredAt = e.ChangedAt })),
            _ => throw new NotSupportedException($"Domain event type not mapped: {domainEvent.GetType().Name}")
        };

        return new OutboxEvent
        {
            Id = domainEvent.EventId,
            EventType = eventType,
            AggregateType = "Tenant",
            AggregateId = aggregateId,
            TenantId = tenantId,
            CorrelationId = _tenantContext.CorrelationId,
            Payload = payload,
            Status = OutboxEventStatus.Pending,
            CreatedAt = domainEvent.OccurredAt
        };
    }

    private static string SerializePayload(object payload)
        => JsonSerializer.Serialize(payload, SerializerOptions);
}

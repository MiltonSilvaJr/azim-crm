using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpportunityPipeline.Domain.Opportunities.Events;
using OpportunityPipeline.Domain.Opportunities.Ports;
using OpportunityPipeline.Infrastructure.Persistence;
using System.Text.Json;

namespace OpportunityPipeline.Infrastructure.Outbox;

/// <summary>
/// Implementação de IDomainEventDispatcher que persiste eventos de domínio
/// no outbox_events na MESMA transação do estado de negócio (ADR-0004).
/// O OutboxPublisherBackgroundService lê os pending e publica no Pub/Sub.
/// Mapeia: ADR-0004, Req 20, design §6.6, TASK-16.
/// </summary>
public sealed class OutboxDomainEventDispatcher(
    OpportunityDbContext context,
    ILogger<OutboxDomainEventDispatcher> logger)
    : IDomainEventDispatcher
{
    /// <inheritdoc/>
    public async Task DispatchAsync(
        DomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        var message = CreateOutboxMessage(domainEvent);
        await context.OutboxMessages.AddAsync(message, cancellationToken).ConfigureAwait(false);

        logger.LogDebug(
            "OutboxDispatcher: evento {EventType} enfileirado (id={EventId}).",
            message.EventType,
            message.Id);
    }

    /// <inheritdoc/>
    public async Task DispatchAllAsync(
        IEnumerable<DomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        var messages = domainEvents.Select(CreateOutboxMessage).ToList();
        await context.OutboxMessages.AddRangeAsync(messages, cancellationToken).ConfigureAwait(false);

        logger.LogDebug(
            "OutboxDispatcher: {Count} evento(s) enfileirado(s).",
            messages.Count);
    }

    private static OutboxMessage CreateOutboxMessage(DomainEvent domainEvent)
    {
        var eventType = GetEventType(domainEvent);
        var payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType());

        return new OutboxMessage
        {
            Id = domainEvent.EventId,
            TenantId = domainEvent.TenantId,
            EventType = eventType,
            Payload = payload,
            Status = "pending",
            CreatedAt = DateTimeOffset.UtcNow,
            Attempts = 0
        };
    }

    private static string GetEventType(DomainEvent domainEvent) =>
        domainEvent switch
        {
            OpportunityCreated => "opportunity.created.v1",
            OpportunityStageChanged => "opportunity.stage_changed.v1",
            OpportunityWon => "opportunity.won.v1",
            OpportunityLost => "opportunity.lost.v1",
            OpportunityStale => "opportunity.stale.v1",
            OpportunityReopened => "opportunity.reopened.v1",
            CommissionCalculated => "commission.calculated.v1",
            CommissionSnapshotCreated => "commission.snapshot_created.v1",
            _ => $"domain.event.{domainEvent.GetType().Name.ToLowerInvariant()}.v1"
        };
}

/// <summary>
/// Background service que lê mensagens pending do Outbox e publica no Pub/Sub.
/// Executa at-least-once com retry backoff. Marca published após confirmação.
/// Mapeia: ADR-0004, design §6.6, TASK-16.
/// </summary>
public sealed class OutboxPublisherBackgroundService(
    ILogger<OutboxPublisherBackgroundService> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 50;
    private const int MaxAttempts = 3;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxPublisher: iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingBatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Encerramento gracioso
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OutboxPublisher: erro no ciclo de publicação.");
            }

            await Task.Delay(PollingInterval, stoppingToken).ConfigureAwait(false);
        }

        logger.LogInformation("OutboxPublisher: encerrado.");
    }

    private async Task ProcessPendingBatchAsync(CancellationToken cancellationToken)
    {
        // Implementação de publicação real dependeria do Pub/Sub SDK (GCP).
        // Para MVP: apenas loga e marca como "published" (stub).
        // Implementação completa será adicionada quando GCP SDK for integrado.
        logger.LogDebug("OutboxPublisher: verificando mensagens pending (stub).");

        // TODO: integrar GCP Pub/Sub SDK aqui (Onda 5/6)
        await Task.CompletedTask.ConfigureAwait(false);
    }
}

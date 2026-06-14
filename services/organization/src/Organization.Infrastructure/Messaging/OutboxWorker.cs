using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Organization.Infrastructure.Persistence;

namespace Organization.Infrastructure.Messaging;

/// <summary>
/// Worker de background que publica eventos da tabela <c>outbox_events</c> para o Pub/Sub.
/// Ciclo: lê batch de eventos não publicados → publica via <see cref="IPubSubPublisher"/> →
/// marca <c>published_at</c> dentro de uma transação (DD-004, §6.6).
/// </summary>
public sealed class OutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxWorker> _logger;
    private readonly TimeSpan _pollingInterval;
    private readonly int _batchSize;

    /// <summary>Inicializa o worker com fábrica de scopes e configuração.</summary>
    public OutboxWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxWorker> logger,
        IOptions<OutboxWorkerOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _pollingInterval = options.Value.PollingInterval;
        _batchSize = options.Value.BatchSize;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxWorker iniciado. Polling a cada {Interval}ms.", _pollingInterval.TotalMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutdown normal — não loga como erro.
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxWorker: erro ao processar batch de eventos. Retentando após {Interval}ms.",
                    _pollingInterval.TotalMilliseconds);
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("OutboxWorker encerrado.");
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPubSubPublisher>();

        // Desabilita o global filter de tenant para que o worker processe eventos de todos os tenants.
        // O worker não tem contexto de tenant corrente — processa o outbox globalmente.
        var accessor = scope.ServiceProvider.GetRequiredService<TenantContextAccessor>();
        accessor.IsEnabled = false;

        // Lê batch de eventos não publicados, ordenados por ocorrência (FIFO por tenant).
        var events = await context.OutboxEvents
            .Where(e => e.PublishedAt == null)
            .OrderBy(e => e.OccurredAt)
            .Take(_batchSize)
            .ToListAsync(cancellationToken);

        if (events.Count == 0)
            return;

        _logger.LogDebug("OutboxWorker: processando {Count} eventos.", events.Count);

        foreach (var outboxEvent in events)
        {
            await PublishEventAsync(publisher, outboxEvent, cancellationToken);
        }

        // Marca todos os eventos do batch como publicados em uma única operação.
        var now = DateTimeOffset.UtcNow;
        foreach (var ev in events)
            ev.PublishedAt = now;

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("OutboxWorker: {Count} eventos publicados.", events.Count);
    }

    private async Task PublishEventAsync(
        IPubSubPublisher publisher,
        OutboxEvent outboxEvent,
        CancellationToken cancellationToken)
    {
        // Tópico derivado do tipo de evento: substituição de pontos por hifens (convenção GCP Pub/Sub).
        var topic = $"org.{outboxEvent.EventType.ToLowerInvariant()}";

        var attributes = new Dictionary<string, string>
        {
            ["event_id"] = outboxEvent.Id.ToString("D"),
            ["event_type"] = outboxEvent.EventType,
            ["tenant_id"] = outboxEvent.TenantId.ToString("D"),
            ["correlation_id"] = outboxEvent.CorrelationId.ToString("D"),
            ["occurred_at"] = outboxEvent.OccurredAt.ToString("O"),
        };

        if (outboxEvent.CausationId.HasValue)
            attributes["causation_id"] = outboxEvent.CausationId.Value.ToString("D");

        try
        {
            await publisher.PublishAsync(topic, outboxEvent.Payload, attributes, cancellationToken);
        }
        catch (Exception ex)
        {
            // Loga o erro mas não aborta o batch — o evento ficará sem published_at e será
            // retentado no próximo ciclo. Não loga o payload para evitar PII acidental.
            _logger.LogError(ex,
                "OutboxWorker: falha ao publicar evento {EventId} (tipo: {EventType}, tenant: {TenantId}).",
                outboxEvent.Id, outboxEvent.EventType, outboxEvent.TenantId);

            throw; // Re-lança para que o evento não seja marcado como publicado.
        }
    }
}

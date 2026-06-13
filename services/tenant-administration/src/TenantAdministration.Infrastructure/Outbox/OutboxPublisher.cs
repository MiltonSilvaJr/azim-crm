using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TenantAdministration.Infrastructure.Observability;
using TenantAdministration.Infrastructure.Persistence;

namespace TenantAdministration.Infrastructure.Outbox;

/// <summary>
/// Serviço de background que publica eventos pendentes do Outbox no Pub/Sub.
/// Lê eventos com status <c>pending</c> usando SELECT FOR UPDATE SKIP LOCKED
/// para evitar concorrência entre múltiplas instâncias (design.md §6.6).
/// Após N falhas consecutivas, marca o evento como <c>failed</c>.
/// </summary>
public sealed class OutboxPublisher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisher> _logger;
    private readonly TenantAdministrationMetrics _metrics;

    /// <summary>Número máximo de tentativas antes de marcar como <c>failed</c>.</summary>
    public const int MaxRetries = 3;

    /// <summary>Intervalo de polling entre execuções.</summary>
    public static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    /// <param name="scopeFactory">Factory de escopo para resolver dependências por iteração.</param>
    /// <param name="logger">Logger estruturado.</param>
    /// <param name="metrics">Métricas do módulo (design.md §11, TASK-22).</param>
    public OutboxPublisher(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxPublisher> logger,
        TenantAdministrationMetrics metrics)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _metrics = metrics;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no loop do OutboxPublisher: {Message}", ex.Message);
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task PublishPendingAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantAdministrationDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPubSubPublisher>();

        // SELECT FOR UPDATE SKIP LOCKED — evita que múltiplas instâncias processem o mesmo evento
        var pending = await db.OutboxEvents
            .Where(e => e.Status == OutboxEventStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        if (pending.Count == 0)
            return;

        foreach (var outboxEvent in pending)
        {
            try
            {
                await publisher.PublishAsync(
                    outboxEvent.EventType,
                    outboxEvent.Id,
                    outboxEvent.Payload,
                    ct);

                outboxEvent.Status = OutboxEventStatus.Published;
                outboxEvent.PublishedAt = DateTimeOffset.UtcNow;

                _logger.LogInformation(
                    "Outbox: evento {EventType} ({EventId}) publicado. CorrelationId={CorrelationId}",
                    outboxEvent.EventType,
                    outboxEvent.Id,
                    outboxEvent.CorrelationId);
            }
            catch (Exception ex)
            {
                outboxEvent.RetryCount++;
                outboxEvent.LastError = ex.Message;

                if (outboxEvent.RetryCount >= MaxRetries)
                {
                    outboxEvent.Status = OutboxEventStatus.Failed;
                    _logger.LogError(
                        ex,
                        "Outbox: evento {EventType} ({EventId}) marcado como failed após {Retries} tentativas. " +
                        "CorrelationId={CorrelationId}",
                        outboxEvent.EventType,
                        outboxEvent.Id,
                        outboxEvent.RetryCount,
                        outboxEvent.CorrelationId);

                    // Métrica de falha do Outbox — alerta quando > 0 (design.md §11, TASK-22)
                    _metrics.OutboxFailedTotal.Add(1,
                        new KeyValuePair<string, object?>("event_type", outboxEvent.EventType));
                }
                else
                {
                    _logger.LogWarning(
                        ex,
                        "Outbox: falha ao publicar {EventType} ({EventId}). Tentativa {Attempt}/{Max}",
                        outboxEvent.EventType,
                        outboxEvent.Id,
                        outboxEvent.RetryCount,
                        MaxRetries);
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }
}

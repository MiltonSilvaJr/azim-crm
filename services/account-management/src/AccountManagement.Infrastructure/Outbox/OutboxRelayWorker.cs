using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AccountManagement.Infrastructure.Persistence;

namespace AccountManagement.Infrastructure.Outbox;

/// <summary>
/// Worker de relay do Outbox transacional.
///
/// Lê mensagens pendentes (<c>published_at IS NULL</c>) e publica no broker externo.
/// Em caso de falha, não marca como publicada — retry na próxima execução (at-least-once).
/// Após confirmação do broker, preenche <c>published_at</c> (DD-007).
///
/// Esta implementação usa um stub/placeholder para o broker (Cloud Pub/Sub).
/// A integração real com GCP Pub/Sub é adicionada via configuração do adaptador.
///
/// Mapeia: design §6.6, DD-007, TASK-11 (ST-03).
/// </summary>
public sealed class OutboxRelayWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxRelayWorker> _logger;
    private readonly IOutboxBrokerPublisher _brokerPublisher;

    /// <summary>Intervalo entre execuções do relay (configurável).</summary>
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    public OutboxRelayWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxRelayWorker> logger,
        IOutboxBrokerPublisher brokerPublisher)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _brokerPublisher = brokerPublisher;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RelayPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Loga sem PII — apenas metadados do erro (RNF 1, DD-003)
                _logger.LogError(ex,
                    "Falha no relay do Outbox. Próxima tentativa em {Interval}s.",
                    PollingInterval.TotalSeconds);
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task RelayPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AccountManagementDbContext>();

        // Lê mensagens pendentes via índice parcial idx_outbox_unpublished (design §7)
        var pending = await context.OutboxMessages
            .Where(m => m.PublishedAt == null)
            .OrderBy(m => m.OccurredAt)
            .Take(50) // lote máximo
            .ToListAsync(cancellationToken);

        foreach (var message in pending)
        {
            try
            {
                await _brokerPublisher.PublishAsync(
                    message.EventType,
                    message.TenantId,
                    message.PayloadJson,
                    message.Id,
                    cancellationToken);

                // Preenche published_at apenas após confirmação do broker (DD-007)
                message.PublishedAt = DateTimeOffset.UtcNow;
                await context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Mensagem do Outbox publicada. EventType={EventType} MessageId={MessageId}",
                    message.EventType, message.Id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Falha na publicação: não marca como publicada → retry na próxima execução
                _logger.LogWarning(ex,
                    "Falha ao publicar mensagem do Outbox. EventType={EventType} MessageId={MessageId}",
                    message.EventType, message.Id);
            }
        }
    }
}

/// <summary>
/// Interface para o broker de publicação de mensagens do Outbox.
/// A implementação concreta publica no Cloud Pub/Sub.
/// Em testes, usa um stub que registra as mensagens publicadas.
///
/// Mapeia: TASK-11 (ST-03), DD-007.
/// </summary>
public interface IOutboxBrokerPublisher
{
    /// <summary>
    /// Publica uma mensagem do Outbox no broker externo.
    /// </summary>
    /// <param name="eventType">Tipo do evento (ex: account.created.v1).</param>
    /// <param name="tenantId">Tenant do evento.</param>
    /// <param name="payloadJson">Payload JSON mascarado (sem PII).</param>
    /// <param name="messageId">Identificador único da mensagem no Outbox.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task PublishAsync(
        string eventType,
        Guid tenantId,
        string payloadJson,
        Guid messageId,
        CancellationToken cancellationToken = default);
}

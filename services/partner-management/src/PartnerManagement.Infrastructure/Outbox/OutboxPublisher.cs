using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PartnerManagement.Application.Ports;
using PartnerManagement.Infrastructure.Persistence;

namespace PartnerManagement.Infrastructure.Outbox;

/// <summary>
/// Relay background que lê mensagens pendentes de <c>outbox_messages</c>
/// e as publica no Pub/Sub (ou log em MVP sem Pub/Sub real).
/// Executa periodicamente; garante idempotência (não republica mensagem já marcada).
/// Entrega at-least-once: consumidores deduplam por <c>event_id</c>.
/// Mapeia: RNF 2, design §6.3, design §6.6, TASK-18.
/// </summary>
public sealed class OutboxPublisher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisher> _logger;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Inicializa o relay com a factory de scopes (necessária para scoped services).
    /// </summary>
    public OutboxPublisher(IServiceScopeFactory scopeFactory, ILogger<OutboxPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Erro ao publicar mensagens do Outbox. Próxima tentativa em {Interval}s",
                    PollingInterval.TotalSeconds);
            }

            await Task.Delay(PollingInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Busca e publica as mensagens pendentes do Outbox.
    /// Usa um contexto com tenant especial (bypass de RLS) para ler todas as mensagens.
    /// </summary>
    private async Task PublishPendingAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();

        // O relay usa um TenantContext que bypass o filtro global para ler de todos os tenants.
        // Isso é necessário pois o relay publica para o Pub/Sub de forma transversal.
        // A RLS não impede o relay porque o relay roda como background service (sem session var).
        // Nota: em produção, o relay precisaria de uma role PostgreSQL com BYPASSRLS.
        PartnerManagementDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<PartnerManagementDbContext>();

        // Busca mensagens não publicadas (sem filtro de tenant — relay é cross-tenant)
        List<OutboxMessage> pending = await dbContext.OutboxMessages
            .IgnoreQueryFilters() // bypass do filtro global de tenant para o relay
            .Where(m => m.PublishedAt == null)
            .OrderBy(m => m.OccurredAt)
            .Take(50) // processa em lotes de 50
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (pending.Count == 0)
        {
            return;
        }

        foreach (OutboxMessage message in pending)
        {
            // Em MVP sem Pub/Sub real: apenas loga o evento publicado
            // Em produção: publicar no Cloud Pub/Sub e marcar como publicado
            _logger.LogInformation(
                "Outbox relay: publicando evento {EventType} (id={MessageId}, tenant={TenantId})",
                message.EventType,
                message.Id,
                message.TenantId);

            message.MarkAsPublished(DateTimeOffset.UtcNow);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

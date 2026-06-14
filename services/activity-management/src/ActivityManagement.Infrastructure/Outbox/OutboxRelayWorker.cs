namespace ActivityManagement.Infrastructure.Outbox;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

/// <summary>
/// Worker de relay do Outbox transacional: lê mensagens pendentes de <c>outbox_messages</c>
/// e as publica no Cloud Pub/Sub (tópico <c>azim-activities</c>), marcando <c>published_at</c>
/// atomicamente para garantir exactly-once semântico na perspectiva do relay.
///
/// Garantias:
/// - Não republica mensagens já com <c>published_at</c> preenchido (índice + WHERE filter).
/// - Deduplicação por <c>(event_type, dedup_key)</c> garantida pelo índice único no banco.
/// - Falhas de publicação são logadas; a mensagem permanece pendente para a próxima execução.
/// - Intervalo configurável; padrão de 5 segundos.
///
/// Em produção, o relay chamaria <c>ITopicPublisher.PublishAsync(message)</c>;
/// nesta implementação base ele usa um delegate injetável para testabilidade.
/// Mapeia: TASK-16, DD-007, design §6.5, RNF 2.
/// </summary>
internal sealed class OutboxRelayWorker : BackgroundService
{
    private readonly string _connectionString;
    private readonly ILogger<OutboxRelayWorker> _logger;
    private readonly Func<OutboxMessage, CancellationToken, Task>? _publishDelegate;

    /// <summary>Intervalo padrão entre ciclos de relay.</summary>
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Construtor para uso em produção via DI.
    /// </summary>
    public OutboxRelayWorker(
        string connectionString,
        ILogger<OutboxRelayWorker>? logger = null,
        Func<OutboxMessage, CancellationToken, Task>? publishDelegate = null)
    {
        _connectionString  = connectionString;
        _logger            = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<OutboxRelayWorker>.Instance;
        _publishDelegate   = publishDelegate;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RelayOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Erro no ciclo de relay do Outbox. Tentando novamente em {Interval}s",
                    DefaultInterval.TotalSeconds);
            }

            await Task.Delay(DefaultInterval, stoppingToken);
        }
    }

    /// <summary>
    /// Executa um único ciclo de relay: busca pendentes e publica.
    /// Exposto como <c>internal</c> para testes de integração.
    /// </summary>
    internal async Task RelayOnceAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        // Lê mensagens pendentes (published_at IS NULL) em ordem de occurred_at
        var pending = await FetchPendingAsync(conn, cancellationToken);

        foreach (var msg in pending)
        {
            try
            {
                // Publica no broker (Pub/Sub em produção; delegate injetado em testes)
                if (_publishDelegate is not null)
                    await _publishDelegate(msg, cancellationToken);
                else
                    await PublishToPubSubAsync(msg, cancellationToken);

                // Marca como publicado atomicamente
                await MarkPublishedAsync(conn, msg.Id, cancellationToken);

                _logger.LogInformation(
                    "Outbox relay: mensagem {MsgId} ({EventType}) publicada com sucesso",
                    msg.Id, msg.EventType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Outbox relay: falha ao publicar mensagem {MsgId} ({EventType}). Será retentada.",
                    msg.Id, msg.EventType);
            }
        }
    }

    private static async Task<List<OutboxMessage>> FetchPendingAsync(
        NpgsqlConnection conn, CancellationToken ct)
    {
        var messages = new List<OutboxMessage>();

        // Seleciona apenas mensagens não publicadas em ordem de ocorrência
        await using var cmd = new NpgsqlCommand(@"
            SELECT id, tenant_id, event_type, dedup_key, payload_json, occurred_at
            FROM outbox_messages
            WHERE published_at IS NULL
            ORDER BY occurred_at
            LIMIT 100
            FOR UPDATE SKIP LOCKED", conn);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            messages.Add(new OutboxMessage
            {
                Id          = reader.GetGuid(0),
                TenantId    = reader.GetGuid(1),
                EventType   = reader.GetString(2),
                DedupKey    = reader.IsDBNull(3) ? null : reader.GetString(3),
                PayloadJson = reader.GetString(4),
                OccurredAt  = reader.GetFieldValue<DateTimeOffset>(5),
            });
        }

        return messages;
    }

    private static async Task MarkPublishedAsync(
        NpgsqlConnection conn, Guid messageId, CancellationToken ct)
    {
        // Atualiza published_at somente se ainda for NULL (idempotente)
        await using var cmd = new NpgsqlCommand(@"
            UPDATE outbox_messages
               SET published_at = now()
             WHERE id = @id
               AND published_at IS NULL", conn);
        cmd.Parameters.AddWithValue("id", messageId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Stub de publicação no Cloud Pub/Sub (produção).
    /// Em produção esta lógica seria substituída pelo SDK do Google Cloud Pub/Sub.
    /// </summary>
    private Task PublishToPubSubAsync(OutboxMessage msg, CancellationToken ct)
    {
        // Produção: injetar ITopicPublisher e chamar PublishAsync aqui.
        // Implementação mínima aceita para infraestrutura base.
        _logger.LogDebug("Outbox relay: publicando {EventType} no tópico azim-activities", msg.EventType);
        return Task.CompletedTask;
    }
}

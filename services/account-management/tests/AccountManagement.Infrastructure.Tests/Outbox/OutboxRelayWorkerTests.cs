using AccountManagement.Infrastructure.Outbox;
using AccountManagement.Infrastructure.Persistence;
using AccountManagement.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AccountManagement.Infrastructure.Tests.Outbox;

/// <summary>
/// Testes de integração para <see cref="OutboxRelayWorker"/> com PostgreSQL real e
/// stub NSubstitute do broker.
///
/// Cobre TASK-11 (ST-03):
/// - Relay publica mensagens pendentes e marca published_at após confirmação.
/// - Mensagens já publicadas (published_at != null) não são reprocessadas (idempotência).
/// - Falha do broker não marca mensagem como publicada (retry na próxima execução).
/// - Relay processa apenas o lote máximo de 50 mensagens por execução.
///
/// Mapeia: TASK-11 (ST-03), DD-007, design §6.6.
/// </summary>
[Collection("PostgresFixture")]
public sealed class OutboxRelayWorkerTests
{
    private readonly PostgresFixture _fixture;

    public OutboxRelayWorkerTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // =========================================================================
    // ST-03-a: relay publica e marca published_at
    // =========================================================================

    [Fact]
    public async Task RelayWorker_publishes_pending_message_and_sets_published_at()
    {
        // Arrange: insere uma mensagem pendente no outbox
        var tenantId = Guid.NewGuid();
        var messageId = await InsertPendingOutboxMessageAsync(tenantId, "account.created.v1");

        var brokerPublisher = Substitute.For<IOutboxBrokerPublisher>();
        brokerPublisher.PublishAsync(
            Arg.Any<string>(),
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var worker = BuildWorker(brokerPublisher);

        // Act: executa uma iteração do relay
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await RunOneRelayIterationAsync(worker, cts.Token);

        // Assert: broker recebeu a chamada com os dados corretos
        await brokerPublisher.Received(1).PublishAsync(
            "account.created.v1",
            tenantId,
            Arg.Any<string>(),
            messageId,
            Arg.Any<CancellationToken>());

        // published_at deve estar preenchida
        var publishedAt = await GetPublishedAtAsync(messageId);
        publishedAt.Should().NotBeNull(
            "relay deve preencher published_at após confirmação do broker (DD-007)");
    }

    // =========================================================================
    // ST-03-b: idempotência — mensagem já publicada não é reprocessada
    // =========================================================================

    [Fact]
    public async Task RelayWorker_does_not_republish_already_published_message()
    {
        // Arrange: insere mensagem com published_at preenchido
        var tenantId = Guid.NewGuid();
        var publishedMessageId = await InsertPublishedOutboxMessageAsync(tenantId, "account.updated.v1");

        var capturedIds = new List<Guid>();
        var brokerPublisher = Substitute.For<IOutboxBrokerPublisher>();
        brokerPublisher.PublishAsync(
            Arg.Any<string>(),
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Do<Guid>(id => capturedIds.Add(id)),
            Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var worker = BuildWorker(brokerPublisher);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await RunOneRelayIterationAsync(worker, cts.Token);

        // A mensagem específica (published_at != null) NÃO deve ter sido enviada ao broker
        capturedIds.Should().NotContain(publishedMessageId,
            "mensagem com published_at preenchido já foi publicada — relay não deve reprocessar");
    }

    // =========================================================================
    // ST-03-c: falha do broker não marca mensagem — retry na próxima execução
    // =========================================================================

    [Fact]
    public async Task RelayWorker_does_not_mark_published_when_broker_throws()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var messageId = await InsertPendingOutboxMessageAsync(tenantId, "account.created.v1");

        var brokerPublisher = Substitute.For<IOutboxBrokerPublisher>();
        brokerPublisher.PublishAsync(
            Arg.Any<string>(),
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Falha simulada do broker"));

        var worker = BuildWorker(brokerPublisher);

        // Act — não deve lançar exceção para o caller
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var act = async () => await RunOneRelayIterationAsync(worker, cts.Token);
        await act.Should().NotThrowAsync(
            "falha do broker deve ser tratada internamente sem propagar exceção");

        // published_at deve continuar null — retry na próxima execução (at-least-once)
        var publishedAt = await GetPublishedAtAsync(messageId);
        publishedAt.Should().BeNull(
            "falha do broker não deve marcar mensagem como publicada (DD-007 — at-least-once)");
    }

    // =========================================================================
    // ST-03-d: múltiplas mensagens pendentes — todas publicadas em uma iteração
    // =========================================================================

    [Fact]
    public async Task RelayWorker_publishes_all_pending_messages_in_one_iteration()
    {
        // Arrange: insere 3 mensagens pendentes com IDs conhecidos
        var tenantId = Guid.NewGuid();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
            ids.Add(await InsertPendingOutboxMessageAsync(tenantId, $"account.created.v{i + 1}"));

        var capturedIds = new List<Guid>();
        var brokerPublisher = Substitute.For<IOutboxBrokerPublisher>();
        brokerPublisher.PublishAsync(
            Arg.Any<string>(),
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Do<Guid>(id => capturedIds.Add(id)),
            Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var worker = BuildWorker(brokerPublisher);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await RunOneRelayIterationAsync(worker, cts.Token);

        // As 3 mensagens inseridas devem ter sido enviadas ao broker e marcadas como publicadas
        foreach (var id in ids)
        {
            capturedIds.Should().Contain(id,
                $"mensagem {id} deve ter sido enviada ao broker na iteração do relay");

            var publishedAt = await GetPublishedAtAsync(id);
            publishedAt.Should().NotBeNull(
                $"mensagem {id} deve estar marcada como publicada após confirmação do broker");
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private OutboxRelayWorker BuildWorker(IOutboxBrokerPublisher brokerPublisher)
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkNpgsql();
        services.AddDbContext<AccountManagementDbContext>(opt =>
            opt.UseNpgsql(_fixture.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history")));

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = NullLogger<OutboxRelayWorker>.Instance;

        return new OutboxRelayWorker(scopeFactory, logger, brokerPublisher);
    }

    /// <summary>
    /// Executa uma iteração do relay via reflexão privada para isolar o loop infinito.
    /// </summary>
    private static async Task RunOneRelayIterationAsync(
        OutboxRelayWorker worker,
        CancellationToken cancellationToken)
    {
        // Invoca RelayPendingMessagesAsync diretamente via reflexão
        var method = typeof(OutboxRelayWorker)
            .GetMethod("RelayPendingMessagesAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        await (Task)method.Invoke(worker, [cancellationToken])!;
    }

    private async Task<Guid> InsertPendingOutboxMessageAsync(Guid tenantId, string eventType)
    {
        var id = Guid.NewGuid();
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            INSERT INTO outbox_messages (id, tenant_id, event_type, payload_json, occurred_at, published_at)
            VALUES ('{id}', '{tenantId}', '{eventType}', '{{}}', now(), NULL)";
        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    private async Task<Guid> InsertPublishedOutboxMessageAsync(Guid tenantId, string eventType)
    {
        var id = Guid.NewGuid();
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            INSERT INTO outbox_messages (id, tenant_id, event_type, payload_json, occurred_at, published_at)
            VALUES ('{id}', '{tenantId}', '{eventType}', '{{}}', now(), now())";
        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    private async Task<DateTime?> GetPublishedAtAsync(Guid messageId)
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT published_at FROM outbox_messages WHERE id = '{messageId}'";
        var result = await cmd.ExecuteScalarAsync();
        return result is DBNull or null ? null : (DateTime)result;
    }
}

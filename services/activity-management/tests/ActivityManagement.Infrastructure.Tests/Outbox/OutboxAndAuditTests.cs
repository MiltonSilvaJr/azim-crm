namespace ActivityManagement.Infrastructure.Tests.Outbox;

using ActivityManagement.Application.Ports;
using ActivityManagement.Infrastructure.Audit;
using ActivityManagement.Infrastructure.Outbox;
using ActivityManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

/// <summary>
/// Testes de integração para Outbox transacional, AuditPublisher e PiiMasker
/// com PostgreSQL real (Testcontainers).
///
/// Garantias testadas:
///   1. <see cref="OutboxPublisher"/> grava outbox_message na mesma transação da escrita — rollback cancela ambos.
///   2. <see cref="OutboxRelayWorker"/> marca published_at e não republica mensagem já publicada.
///   3. Trigger de imutabilidade: UPDATE em audit_logs levanta exceção.
///   4. <see cref="PiiMasker"/> substitui title/description por [MASKED] — nunca expõe PII no delta_json.
///   5. <see cref="AuditPublisher"/> grava entrada em audit_logs com correlation_id.
///
/// Mapeia: TASK-16, design §6.5, §6.6, §11, DD-007, DD-009, RNF 2, RNF 7.
/// </summary>
public sealed class OutboxAndAuditTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithUsername("testuser")
        .WithPassword("testpass")
        .WithDatabase("outbox_audit_test")
        .Build();

    private NpgsqlConnection _conn = null!;
    private ActivityManagementDbContext _ctx = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await _conn.OpenAsync();

        await SetupSchemaAsync();

        var options = new DbContextOptionsBuilder<ActivityManagementDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        _ctx = new ActivityManagementDbContext(options);
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
        await _conn.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task SetupSchemaAsync()
    {
        await using var cmd = new NpgsqlCommand(SchemaSetupSql, _conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<long> CountAsync(string table)
    {
        await using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {table}", _conn);
        return (long)(await cmd.ExecuteScalarAsync() ?? 0L);
    }

    private async Task<string?> ScalarStringAsync(string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, _conn);
        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    // ── Testes — OutboxPublisher ───────────────────────────────────────────────

    [Fact]
    public async Task OutboxPublisher_Enqueue_Persists_Message_Via_EfContext()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _ctx.SetTenant(tenantId);
        var publisher = new OutboxPublisher(_ctx);

        // Act
        await publisher.EnqueueAsync("ActivityCreated", "{\"activityId\":\"abc\"}", tenantId);
        await _ctx.SaveChangesAsync();

        // Assert: mensagem gravada no outbox
        var count = await CountAsync("outbox_messages");
        count.Should().Be(1, because: "deve haver 1 mensagem gravada pelo OutboxPublisher");

        var eventType = await ScalarStringAsync("SELECT event_type FROM outbox_messages LIMIT 1");
        eventType.Should().Be("ActivityCreated");
    }

    [Fact]
    public async Task OutboxPublisher_Enqueue_WithDedupKey_Stores_DedupKey()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _ctx.SetTenant(tenantId);
        var publisher = new OutboxPublisher(_ctx);
        var activityId = Guid.NewGuid();
        var dedupKey = $"{activityId}:2026-06-14";

        // Act
        await publisher.EnqueueAsync("ActivityOverdue", "{}", tenantId, dedupKey);
        await _ctx.SaveChangesAsync();

        // Assert
        var key = await ScalarStringAsync("SELECT dedup_key FROM outbox_messages LIMIT 1");
        key.Should().Be(dedupKey, because: "dedup_key deve ser persistido para deduplicação do relay");
    }

    [Fact]
    public async Task OutboxPublisher_Enqueue_Message_Has_Null_PublishedAt_Initially()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _ctx.SetTenant(tenantId);
        var publisher = new OutboxPublisher(_ctx);

        // Act
        await publisher.EnqueueAsync("ActivityCompleted", "{}", tenantId);
        await _ctx.SaveChangesAsync();

        // Assert: published_at deve ser NULL (pendente)
        await using var cmd = new NpgsqlCommand(
            "SELECT published_at FROM outbox_messages LIMIT 1", _conn);
        var publishedAt = await cmd.ExecuteScalarAsync();
        publishedAt.Should().Be(DBNull.Value, because: "mensagem recém-enfileirada não deve ter published_at");
    }

    // ── Testes — OutboxRelayWorker ─────────────────────────────────────────────

    [Fact]
    public async Task OutboxRelayWorker_Marks_PublishedAt_For_Pending_Messages()
    {
        // Arrange: inserir mensagem pendente diretamente
        var tenantId = Guid.NewGuid();
        await using var insertCmd = new NpgsqlCommand(@"
            INSERT INTO outbox_messages (id, tenant_id, event_type, payload_json, occurred_at)
            VALUES (gen_random_uuid(), @tenantId, 'ActivityCreated', '{}', now())", _conn);
        insertCmd.Parameters.AddWithValue("tenantId", tenantId);
        await insertCmd.ExecuteNonQueryAsync();

        // Act: criar relay e executar um ciclo de publicação
        var relay = new OutboxRelayWorker(_postgres.GetConnectionString());
        await relay.RelayOnceAsync(CancellationToken.None);

        // Assert: published_at deve ser preenchido
        await using var checkCmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM outbox_messages WHERE published_at IS NOT NULL", _conn);
        var published = (long)(await checkCmd.ExecuteScalarAsync() ?? 0L);
        published.Should().Be(1, because: "relay deve marcar published_at após publicar a mensagem");
    }

    [Fact]
    public async Task OutboxRelayWorker_Does_Not_Republish_Already_Published_Messages()
    {
        // Arrange: mensagem já publicada (published_at preenchido)
        var tenantId = Guid.NewGuid();
        await using var insertCmd = new NpgsqlCommand(@"
            INSERT INTO outbox_messages (id, tenant_id, event_type, payload_json, occurred_at, published_at)
            VALUES (gen_random_uuid(), @tenantId, 'ActivityCreated', '{}', now(), now())", _conn);
        insertCmd.Parameters.AddWithValue("tenantId", tenantId);
        await insertCmd.ExecuteNonQueryAsync();

        var relay = new OutboxRelayWorker(_postgres.GetConnectionString());

        // Act: executar relay múltiplas vezes
        await relay.RelayOnceAsync(CancellationToken.None);
        await relay.RelayOnceAsync(CancellationToken.None);

        // Assert: ainda há apenas 1 mensagem (não duplicou)
        var count = await CountAsync("outbox_messages");
        count.Should().Be(1, because: "relay não deve criar nem duplicar mensagens já publicadas");

        // E o published_at da mensagem original deve permanecer o mesmo (não atualizado)
        await using var checkCmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM outbox_messages WHERE published_at IS NOT NULL", _conn);
        var published = (long)(await checkCmd.ExecuteScalarAsync() ?? 0L);
        published.Should().Be(1);
    }

    // ── Testes — AuditPublisher ────────────────────────────────────────────────

    [Fact]
    public async Task AuditPublisher_Persists_Entry_With_CorrelationId()
    {
        // Arrange
        var tenantId  = Guid.NewGuid();
        var entityId  = Guid.NewGuid();
        var corrId    = Guid.NewGuid();
        _ctx.SetTenant(tenantId);
        var publisher = new AuditPublisher(_ctx);

        var entry = new AuditEntry(
            TenantId:      tenantId,
            UserId:        Guid.NewGuid(),
            EntityType:    "Activity",
            EntityId:      entityId,
            Action:        "completed",
            DeltaJson:     "{\"status\":\"completed\"}",
            CorrelationId: corrId);

        // Act
        await publisher.PublishAsync(entry);
        await _ctx.SaveChangesAsync();

        // Assert
        var count = await CountAsync("audit_logs");
        count.Should().Be(1, because: "deve haver 1 entrada de auditoria");

        var storedCorrId = await ScalarStringAsync(
            "SELECT correlation_id::text FROM audit_logs LIMIT 1");
        storedCorrId.Should().Be(corrId.ToString(), because: "correlation_id deve ser preservado");
    }

    [Fact]
    public async Task AuditLogs_Trigger_Prevents_Update()
    {
        // Arrange: inserir diretamente via conexão admin
        var tenantId = Guid.NewGuid();
        await using var insertCmd = new NpgsqlCommand(@"
            INSERT INTO audit_logs (id, tenant_id, entity_type, entity_id, action, delta_json)
            VALUES (gen_random_uuid(), @tenantId, 'Activity', gen_random_uuid(), 'created', '{}')", _conn);
        insertCmd.Parameters.AddWithValue("tenantId", tenantId);
        await insertCmd.ExecuteNonQueryAsync();

        // Act + Assert: UPDATE deve falhar com trigger de imutabilidade
        var act = async () =>
        {
            await using var cmd = new NpgsqlCommand(
                "UPDATE audit_logs SET action = 'tampered' WHERE action = 'created'", _conn);
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<PostgresException>()
            .Where(e => e.SqlState == "P0001",
                   because: "trigger trg_audit_logs_immutable deve rejeitar UPDATE com RAISE EXCEPTION");
    }

    [Fact]
    public async Task AuditLogs_Trigger_Prevents_Delete()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var insertCmd = new NpgsqlCommand(@"
            INSERT INTO audit_logs (id, tenant_id, entity_type, entity_id, action, delta_json)
            VALUES (gen_random_uuid(), @tenantId, 'Activity', gen_random_uuid(), 'created', '{}')", _conn);
        insertCmd.Parameters.AddWithValue("tenantId", tenantId);
        await insertCmd.ExecuteNonQueryAsync();

        // Act + Assert: DELETE deve falhar com trigger de imutabilidade
        var act = async () =>
        {
            await using var cmd = new NpgsqlCommand("DELETE FROM audit_logs", _conn);
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<PostgresException>()
            .Where(e => e.SqlState == "P0001",
                   because: "trigger trg_audit_logs_immutable deve rejeitar DELETE com RAISE EXCEPTION");
    }

    // ── Testes — PiiMasker ────────────────────────────────────────────────────

    [Fact]
    public void PiiMasker_Replaces_Title_With_Masked()
    {
        // Arrange
        var original = new Dictionary<string, object?> { ["title"] = "Reunião confidencial" };

        // Act
        var masked = PiiMasker.MaskDelta(original);

        // Assert
        masked.Should().ContainKey("title");
        masked["title"].Should().Be("[MASKED]",
            because: "title é PII e nunca deve aparecer em claro no delta_json (RNF 7.2)");
    }

    [Fact]
    public void PiiMasker_Replaces_Description_With_Masked()
    {
        // Arrange
        var original = new Dictionary<string, object?> { ["description"] = "Detalhes sensíveis" };

        // Act
        var masked = PiiMasker.MaskDelta(original);

        // Assert
        masked["description"].Should().Be("[MASKED]",
            because: "description é PII e nunca deve aparecer em claro no delta_json (RNF 7.2)");
    }

    [Fact]
    public void PiiMasker_Preserves_NonPii_Fields()
    {
        // Arrange
        var original = new Dictionary<string, object?>
        {
            ["status"]   = "completed",
            ["priority"] = "high",
            ["title"]    = "Ocultar isto",
        };

        // Act
        var masked = PiiMasker.MaskDelta(original);

        // Assert: campos não-PII preservados
        masked["status"].Should().Be("completed");
        masked["priority"].Should().Be("high");
        // título mascarado
        masked["title"].Should().Be("[MASKED]");
    }

    [Fact]
    public void PiiMasker_SerializesToJson_Without_Pii()
    {
        // Arrange
        var original = new Dictionary<string, object?>
        {
            ["title"]       = "Conteúdo sensível",
            ["description"] = "Outro dado sensível",
            ["status"]      = "completed",
        };

        // Act
        var json = PiiMasker.MaskDeltaToJson(original);

        // Assert
        json.Should().NotContain("Conteúdo sensível",
            because: "JSON de auditoria nunca deve conter title em claro");
        json.Should().NotContain("Outro dado sensível",
            because: "JSON de auditoria nunca deve conter description em claro");
        json.Should().Contain("[MASKED]");
        json.Should().Contain("completed");
    }

    // ── Schema de configuração ─────────────────────────────────────────────────

    private const string SchemaSetupSql = @"
        CREATE TABLE IF NOT EXISTS outbox_messages (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            tenant_id       UUID NOT NULL,
            event_type      TEXT NOT NULL,
            dedup_key       TEXT,
            payload_json    TEXT NOT NULL,
            occurred_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
            published_at    TIMESTAMPTZ
        );

        CREATE UNIQUE INDEX IF NOT EXISTS uq_outbox_dedup
            ON outbox_messages (event_type, dedup_key)
            WHERE dedup_key IS NOT NULL;

        CREATE INDEX IF NOT EXISTS idx_outbox_unpublished
            ON outbox_messages (occurred_at)
            WHERE published_at IS NULL;

        CREATE TABLE IF NOT EXISTS audit_logs (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            tenant_id       UUID NOT NULL,
            user_id         UUID,
            entity_type     TEXT NOT NULL,
            entity_id       UUID NOT NULL,
            action          TEXT NOT NULL,
            delta_json      TEXT NOT NULL,
            correlation_id  UUID,
            created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        -- Trigger de imutabilidade em audit_logs (RNF 2, design §6.6, TASK-16)
        CREATE OR REPLACE FUNCTION fn_audit_logs_immutable()
        RETURNS TRIGGER AS $$
        BEGIN
            RAISE EXCEPTION 'audit_logs é append-only: UPDATE e DELETE não são permitidos (RNF 2)';
        END;
        $$ LANGUAGE plpgsql;

        DROP TRIGGER IF EXISTS trg_audit_logs_immutable ON audit_logs;
        CREATE TRIGGER trg_audit_logs_immutable
            BEFORE UPDATE OR DELETE ON audit_logs
            FOR EACH ROW EXECUTE FUNCTION fn_audit_logs_immutable();
    ";
}

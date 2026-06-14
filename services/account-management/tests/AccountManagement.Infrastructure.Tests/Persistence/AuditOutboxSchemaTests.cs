using Npgsql;

namespace AccountManagement.Infrastructure.Tests.Persistence;

/// <summary>
/// Testes de integração (TASK-09) verificando schema de <c>audit_logs</c>,
/// <c>outbox_messages</c> e <c>idempotency_keys</c>.
///
/// Valida:
/// - Colunas de <c>audit_logs</c> sem <c>updated_at</c> (append-only — RNF 8.1).
/// - UPDATE em <c>audit_logs</c> falha (trigger de imutabilidade).
/// - Índice parcial <c>idx_outbox_unpublished</c> existe.
/// - PK composta de <c>idempotency_keys</c> existe.
///
/// Mapeia: TASK-09 (ST-01, ST-02), design §7, RNF 8.
/// </summary>
[Collection("PostgresFixture")]
public sealed class AuditOutboxSchemaTests
{
    private readonly PostgresFixture _fixture;

    public AuditOutboxSchemaTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AuditLogs_has_correct_columns_without_updated_at()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT column_name
            FROM information_schema.columns
            WHERE table_name = 'audit_logs'
            ORDER BY column_name";

        var columns = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(0));

        Assert.Contains("id", columns);
        Assert.Contains("tenant_id", columns);
        Assert.Contains("user_id", columns);
        Assert.Contains("entity_type", columns);
        Assert.Contains("entity_id", columns);
        Assert.Contains("action", columns);
        Assert.Contains("delta_json", columns);
        Assert.Contains("created_at", columns);
        Assert.DoesNotContain("updated_at", columns); // append-only — RNF 8.1
    }

    [Fact]
    public async Task AuditLogs_immutability_trigger_prevents_update()
    {
        // ST-01 TASK-09: INSERT permitido; UPDATE → exceção do trigger
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();

        var entryId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        // INSERT deve funcionar
        await using var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = $@"
            INSERT INTO audit_logs (id, tenant_id, user_id, entity_type, entity_id, action, delta_json, created_at)
            VALUES ('{entryId}', '{tenantId}', '{userId}', 'Account', '{entityId}', 'created', '{{}}', now())";
        await insertCmd.ExecuteNonQueryAsync();

        // UPDATE deve falhar com o trigger
        await using var updateCmd = conn.CreateCommand();
        updateCmd.CommandText = $@"UPDATE audit_logs SET action = 'updated' WHERE id = '{entryId}'";

        var ex = await Assert.ThrowsAsync<PostgresException>(
            async () => await updateCmd.ExecuteNonQueryAsync());

        Assert.Contains("append-only", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuditLogs_trigger_trg_audit_logs_immutable_exists()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM information_schema.triggers
            WHERE trigger_name = 'trg_audit_logs_immutable'
              AND event_object_table = 'audit_logs'";

        var count = (long)(await cmd.ExecuteScalarAsync())!;
        // O trigger pode aparecer múltiplas vezes no information_schema.triggers
        // (uma entrada por evento: UPDATE, DELETE — TRUNCATE pode ou não aparecer).
        // O importante é que exista ao menos uma entrada (trigger criado).
        Assert.True(count >= 1L, $"Esperado ao menos 1 entrada do trigger, mas encontrado {count}");
    }

    [Fact]
    public async Task AuditLogs_index_idx_audit_logs_tenant_entity_exists()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM pg_indexes
            WHERE tablename = 'audit_logs'
              AND indexname = 'idx_audit_logs_tenant_entity'";

        var count = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(1L, count);
    }

    [Fact]
    public async Task OutboxMessages_has_partial_index_idx_outbox_unpublished()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT indexdef FROM pg_indexes
            WHERE tablename = 'outbox_messages'
              AND indexname = 'idx_outbox_unpublished'";

        var indexDef = (string?)(await cmd.ExecuteScalarAsync());
        Assert.NotNull(indexDef);
        Assert.Contains("published_at IS NULL", indexDef, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IdempotencyKeys_has_composite_primary_key()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM information_schema.key_column_usage kcu
            JOIN information_schema.table_constraints tc
              ON kcu.constraint_name = tc.constraint_name
             AND kcu.table_name = tc.table_name
            WHERE kcu.table_name = 'idempotency_keys'
              AND tc.constraint_type = 'PRIMARY KEY'";

        var count = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(2L, count); // PK composta: tenant_id + idempotency_key
    }
}

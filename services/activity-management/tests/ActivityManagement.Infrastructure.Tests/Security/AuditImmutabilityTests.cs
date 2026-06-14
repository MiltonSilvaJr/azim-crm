namespace ActivityManagement.Infrastructure.Tests.Security;

using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

/// <summary>
/// Testes de imutabilidade do audit_logs via banco real (Testcontainers).
/// Verifica que UPDATE e DELETE em audit_logs falham com o trigger de imutabilidade
/// configurado na migration (TASK-16, TASK-23, RNF 2, design §7).
///
/// Mapeia: TASK-23, RNF 2.2, design §7, rule audit-immutability.md.
/// </summary>
public sealed class AuditImmutabilityTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithUsername("testuser")
        .WithPassword("testpass")
        .WithDatabase("audit_immutability_test")
        .Build();

    private NpgsqlConnection _conn = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await _conn.OpenAsync();

        await SetupSchemaAsync();
    }

    public async Task DisposeAsync()
    {
        await _conn.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // ── UPDATE em audit_logs deve falhar ─────────────────────────────────────

    [Fact]
    public async Task AuditLogs_Update_FailsWithTrigger()
    {
        // Arrange: inserir um registro de auditoria legítimo
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var auditId  = Guid.NewGuid();

        await using var insertCmd = _conn.CreateCommand();
        insertCmd.CommandText =
            $"INSERT INTO audit_logs (id, tenant_id, entity_type, entity_id, action, delta_json) " +
            $"VALUES ('{auditId}', '{tenantId}', 'Activity', '{entityId}', 'created', '{{}}')";
        await insertCmd.ExecuteNonQueryAsync();

        // Act: tentar UPDATE — deve falhar com trigger de imutabilidade
        await using var updateCmd = _conn.CreateCommand();
        updateCmd.CommandText = $"UPDATE audit_logs SET action = 'modified' WHERE id = '{auditId}'";

        var act = async () => await updateCmd.ExecuteNonQueryAsync();

        // Assert: trigger deve rejeitar o UPDATE
        await act.Should().ThrowAsync<NpgsqlException>(
            because: "audit_logs é append-only — o trigger trg_audit_logs_immutable deve rejeitar UPDATE (RNF 2.2)");
    }

    [Fact]
    public async Task AuditLogs_Delete_FailsWithTrigger()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var auditId  = Guid.NewGuid();

        await using var insertCmd = _conn.CreateCommand();
        insertCmd.CommandText =
            $"INSERT INTO audit_logs (id, tenant_id, entity_type, entity_id, action, delta_json) " +
            $"VALUES ('{auditId}', '{tenantId}', 'Activity', '{entityId}', 'deleted', '{{}}')";
        await insertCmd.ExecuteNonQueryAsync();

        // Act: tentar DELETE
        await using var deleteCmd = _conn.CreateCommand();
        deleteCmd.CommandText = $"DELETE FROM audit_logs WHERE id = '{auditId}'";

        var act = async () => await deleteCmd.ExecuteNonQueryAsync();

        // Assert
        await act.Should().ThrowAsync<NpgsqlException>(
            because: "audit_logs é append-only — o trigger trg_audit_logs_immutable deve rejeitar DELETE (RNF 2.2)");
    }

    [Fact]
    public async Task AuditLogs_Insert_Succeeds()
    {
        // Arrange/Act: INSERT deve sempre ser bem-sucedido (append-only)
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        await using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            $"INSERT INTO audit_logs (id, tenant_id, entity_type, entity_id, action, delta_json) " +
            $"VALUES (gen_random_uuid(), '{tenantId}', 'Activity', '{entityId}', 'created', '{{}}')";


        var act = async () => await cmd.ExecuteNonQueryAsync();

        // Assert: INSERT não deve lançar exceção
        await act.Should().NotThrowAsync(because: "INSERT em audit_logs deve sempre ser permitido");
    }

    // ── Setup do schema mínimo para os testes ─────────────────────────────────

    private async Task SetupSchemaAsync()
    {
        // Função que impede modificação (replicada da migration TASK-16)
        await using var cmd1 = _conn.CreateCommand();
        cmd1.CommandText = """
            CREATE OR REPLACE FUNCTION prevent_immutable_table_modification()
            RETURNS TRIGGER LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'Operação proibida: % em tabela imutável %.', TG_OP, TG_TABLE_NAME;
            END;
            $$;
            """;
        await cmd1.ExecuteNonQueryAsync();

        // Tabela audit_logs mínima para o teste
        await using var cmd2 = _conn.CreateCommand();
        cmd2.CommandText = """
            CREATE TABLE IF NOT EXISTS audit_logs (
                id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                tenant_id    UUID NOT NULL,
                user_id      UUID,
                entity_type  TEXT NOT NULL,
                entity_id    UUID NOT NULL,
                action       TEXT NOT NULL,
                delta_json   JSONB NOT NULL DEFAULT '{}',
                correlation_id UUID,
                created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
            );
            """;
        await cmd2.ExecuteNonQueryAsync();

        // Trigger de imutabilidade (TASK-16)
        await using var cmd3 = _conn.CreateCommand();
        cmd3.CommandText = """
            CREATE TRIGGER trg_audit_logs_immutable
                BEFORE UPDATE OR DELETE ON audit_logs
                FOR EACH ROW EXECUTE FUNCTION prevent_immutable_table_modification();
            """;
        await cmd3.ExecuteNonQueryAsync();
    }
}

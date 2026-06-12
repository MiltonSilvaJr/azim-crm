using AuditLog.Infrastructure.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace AuditLog.Infrastructure.Tests.Persistence;

/// <summary>
/// Testes de integração com Testcontainers (PostgreSQL real) para verificar:
/// — Estrutura da tabela e colunas (TASK-12).
/// — CHECK constraints (action, entity_type).
/// — Trigger de imutabilidade (trg_audit_logs_immutable).
/// — RLS habilitado e ativo.
/// — Índices presentes.
/// — UPDATE lança exceção (RNF-001.1, RNF-001.2).
/// </summary>
[Collection(PostgresTestCollection.Name)]
public sealed class ImmutableMigrationTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;

    public ImmutableMigrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using var ctx = TestDbContextFactory.Create(
            _fixture.SuperuserConnectionString, Guid.NewGuid());
        await ctx.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ------------------------------------------------------------------ Estrutura da tabela

    [Fact]
    public async Task Table_AuditLogs_Exists()
    {
        var exists = await ExecuteScalarAsync<bool>(_fixture.SuperuserConnectionString,
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'audit_logs');");
        exists.Should().BeTrue("a tabela audit_logs deve existir após a migration");
    }

    [Theory]
    [InlineData("id")]
    [InlineData("tenant_id")]
    [InlineData("user_id")]
    [InlineData("entity_type")]
    [InlineData("entity_id")]
    [InlineData("action")]
    [InlineData("delta_json")]
    [InlineData("created_at")]
    public async Task Column_Exists_And_NotNull(string columnName)
    {
        var isNullable = await ExecuteScalarAsync<string>(_fixture.SuperuserConnectionString,
            $"SELECT is_nullable FROM information_schema.columns " +
            $"WHERE table_name = 'audit_logs' AND column_name = '{columnName}';");

        isNullable.Should().Be("NO",
            $"a coluna '{columnName}' deve ser NOT NULL (REQ-002)");
    }

    [Fact]
    public async Task Column_UpdatedAt_DoesNotExist()
    {
        var exists = await ExecuteScalarAsync<bool>(_fixture.SuperuserConnectionString,
            "SELECT EXISTS (SELECT 1 FROM information_schema.columns " +
            "WHERE table_name = 'audit_logs' AND column_name = 'updated_at');");
        exists.Should().BeFalse("audit_logs não deve ter updated_at (REQ-002.5)");
    }

    // ------------------------------------------------------------------ CHECK constraints

    [Fact]
    public async Task CheckConstraint_Action_Exists()
    {
        var count = await ExecuteScalarAsync<long>(_fixture.SuperuserConnectionString,
            "SELECT COUNT(*) FROM information_schema.table_constraints " +
            "WHERE table_name = 'audit_logs' " +
            "AND constraint_type = 'CHECK' " +
            "AND constraint_name = 'chk_audit_logs_action';");

        count.Should().Be(1, "deve existir a CHECK constraint chk_audit_logs_action");
    }

    [Fact]
    public async Task CheckConstraint_EntityTypeNotEmpty_Exists()
    {
        var count = await ExecuteScalarAsync<long>(_fixture.SuperuserConnectionString,
            "SELECT COUNT(*) FROM information_schema.table_constraints " +
            "WHERE table_name = 'audit_logs' " +
            "AND constraint_type = 'CHECK' " +
            "AND constraint_name = 'chk_audit_logs_entity_type_not_empty';");

        count.Should().Be(1, "deve existir a CHECK constraint chk_audit_logs_entity_type_not_empty");
    }

    // ------------------------------------------------------------------ Trigger

    [Fact]
    public async Task Trigger_ImmutableAuditLogs_Exists()
    {
        var count = await ExecuteScalarAsync<long>(_fixture.SuperuserConnectionString,
            "SELECT COUNT(*) FROM information_schema.triggers " +
            "WHERE event_object_table = 'audit_logs' " +
            "AND trigger_name = 'trg_audit_logs_immutable';");

        count.Should().BeGreaterThan(0, "o trigger trg_audit_logs_immutable deve existir (RNF-001, DD-002)");
    }

    [Fact]
    public async Task Trigger_Update_ThrowsException()
    {
        var id = await InsertTestRecordAsync();

        var act = async () =>
        {
            await using var conn = new NpgsqlConnection(_fixture.SuperuserConnectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"UPDATE audit_logs SET action = 'delete' WHERE id = '{id}';";
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<PostgresException>(
            "o trigger deve rejeitar UPDATE mesmo do superuser (RNF-001.1)");

        await _fixture.TruncateAuditLogsAsync();
    }

    [Fact]
    public async Task Trigger_Delete_ThrowsException()
    {
        var id = await InsertTestRecordAsync();

        var act = async () =>
        {
            await using var conn = new NpgsqlConnection(_fixture.SuperuserConnectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DELETE FROM audit_logs WHERE id = '{id}';";
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<PostgresException>(
            "o trigger deve rejeitar DELETE (RNF-001.1, DD-002)");

        await _fixture.TruncateAuditLogsAsync();
    }

    // ------------------------------------------------------------------ RLS

    [Fact]
    public async Task Rls_IsEnabled_OnAuditLogs()
    {
        var isEnabled = await ExecuteScalarAsync<bool>(_fixture.SuperuserConnectionString,
            "SELECT rowsecurity FROM pg_tables WHERE tablename = 'audit_logs';");

        isEnabled.Should().BeTrue("RLS deve estar habilitado em audit_logs (REQ-005, DD-003)");
    }

    [Fact]
    public async Task Rls_Policy_Exists()
    {
        var count = await ExecuteScalarAsync<long>(_fixture.SuperuserConnectionString,
            "SELECT COUNT(*) FROM pg_policies " +
            "WHERE tablename = 'audit_logs' AND policyname = 'rls_audit_logs_tenant';");

        count.Should().Be(1, "a policy rls_audit_logs_tenant deve existir (DD-003)");
    }

    // ------------------------------------------------------------------ Índices

    [Theory]
    [InlineData("ix_audit_logs_tenant_entity")]
    [InlineData("ix_audit_logs_tenant_created")]
    [InlineData("ix_audit_logs_tenant_user")]
    public async Task Index_Exists(string indexName)
    {
        var count = await ExecuteScalarAsync<long>(_fixture.SuperuserConnectionString,
            $"SELECT COUNT(*) FROM pg_indexes " +
            $"WHERE tablename = 'audit_logs' AND indexname = '{indexName}';");

        count.Should().Be(1, $"o índice {indexName} deve existir (design §7.1)");
    }

    // ------------------------------------------------------------------ Helpers

    private async Task<Guid> InsertTestRecordAsync()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        await using var conn = new NpgsqlConnection(_fixture.SuperuserConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        const string deltaJson = """{"kind":"create","after":{"name":"test"}}""";
        cmd.CommandText =
            $"INSERT INTO audit_logs (id, tenant_id, user_id, entity_type, entity_id, action, delta_json) " +
            $"VALUES ('{id}', '{tenantId}', '{userId}', 'TestEntity', '{entityId}', 'create', '{deltaJson}');";
        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    private static async Task<T> ExecuteScalarAsync<T>(string connectionString, string sql)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var result = await cmd.ExecuteScalarAsync();
        return (T)Convert.ChangeType(result!, typeof(T));
    }
}

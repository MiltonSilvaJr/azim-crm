namespace ActivityManagement.Infrastructure.Tests.Persistence;

using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

/// <summary>
/// Testes da migration DD-001 (<see cref="AddActivityStatusPriority"/>).
/// Valida: backfill de status/priority, constraints de domínio,
/// constraint de consistência I5 e índice único do token_hash.
/// Mapeia: TASK-14, DD-001, design §7, Req 4, PBT-01.
/// </summary>
public sealed class MigrationDd001Tests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithUsername("testdd001")
        .WithPassword("testpass")
        .WithDatabase("dd001_test")
        .Build();

    private NpgsqlConnection _conn = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await _conn.OpenAsync();

        // Schema base (sem status/priority ainda — simula estado antes da DD-001)
        await ExecuteAsync(@"
            CREATE TABLE activities (
                id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                tenant_id       UUID NOT NULL,
                bu_id           UUID NOT NULL,
                owner_id        UUID NOT NULL,
                activity_type   VARCHAR(20) NOT NULL DEFAULT 'meeting',
                title           TEXT NOT NULL,
                due_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
                completed_at    TIMESTAMPTZ,
                created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
                updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            -- ADR-0006: schema canônico do digest (BYTEA, activity_id NOT NULL)
            -- Esta tabela existe no banco compartilhado e é criada pelo digest antes deste serviço.
            CREATE TABLE digest_action_tokens (
                id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                tenant_id   UUID NOT NULL,
                user_id     UUID NOT NULL,
                activity_id UUID NOT NULL,
                action      VARCHAR(20) NOT NULL DEFAULT 'Complete',
                token_hash  BYTEA NOT NULL,
                expires_at  TIMESTAMPTZ NOT NULL DEFAULT now() + INTERVAL '24 hours',
                created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
            );
        ");
    }

    public async Task DisposeAsync()
    {
        await _conn.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task ExecuteAsync(string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, _conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, _conn);
        return await cmd.ExecuteScalarAsync();
    }

    private async Task ApplyMigrationDd001Async()
    {
        // Aplica a migration DD-001 (replica o Up() da migration)
        await ExecuteAsync(@"
            ALTER TABLE activities
                ADD COLUMN IF NOT EXISTS status   VARCHAR(20),
                ADD COLUMN IF NOT EXISTS priority VARCHAR(10);

            UPDATE activities SET status = 'completed' WHERE completed_at IS NOT NULL AND status IS NULL;
            UPDATE activities SET status = 'pending'   WHERE status IS NULL;
            UPDATE activities SET priority = 'medium'  WHERE priority IS NULL;

            ALTER TABLE activities
                ALTER COLUMN status   SET NOT NULL,
                ALTER COLUMN status   SET DEFAULT 'pending',
                ALTER COLUMN priority SET NOT NULL,
                ALTER COLUMN priority SET DEFAULT 'medium';

            ALTER TABLE activities
                ADD CONSTRAINT chk_activities_status
                    CHECK (status IN ('pending','in_progress','completed','cancelled')),
                ADD CONSTRAINT chk_activities_priority
                    CHECK (priority IN ('low','medium','high')),
                ADD CONSTRAINT chk_activities_completed_consistency
                    CHECK ((status = 'completed') = (completed_at IS NOT NULL));

            CREATE UNIQUE INDEX IF NOT EXISTS uq_digest_action_tokens_hash
                ON digest_action_tokens (token_hash);
        ");
    }

    // ── Testes ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Backfill_Row_With_CompletedAt_Gets_Status_Completed()
    {
        // Arrange: inserir linha com completed_at preenchido
        var completedAt = DateTimeOffset.UtcNow.AddDays(-1);
        await ExecuteAsync($@"
            INSERT INTO activities (id, tenant_id, bu_id, owner_id, title, completed_at)
            VALUES (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(),
                    gen_random_uuid(), 'Reunião concluída', '{completedAt:O}');
        ");

        // Act: aplicar migration
        await ApplyMigrationDd001Async();

        // Assert: status deve ser 'completed'
        var status = await ScalarAsync(
            "SELECT status FROM activities WHERE completed_at IS NOT NULL LIMIT 1");
        status.Should().Be("completed");
    }

    [Fact]
    public async Task Backfill_Row_Without_CompletedAt_Gets_Status_Pending()
    {
        // Arrange: inserir linha sem completed_at
        await ExecuteAsync(@"
            INSERT INTO activities (id, tenant_id, bu_id, owner_id, title)
            VALUES (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(),
                    gen_random_uuid(), 'Atividade pendente');
        ");

        // Act
        await ApplyMigrationDd001Async();

        // Assert
        var status = await ScalarAsync(
            "SELECT status FROM activities WHERE completed_at IS NULL LIMIT 1");
        status.Should().Be("pending");
    }

    [Fact]
    public async Task Constraint_Rejects_Completed_Status_With_Null_CompletedAt()
    {
        // Arrange
        await ApplyMigrationDd001Async();

        // Act + Assert: deve falhar na constraint I5
        var act = async () => await ExecuteAsync(@"
            INSERT INTO activities (id, tenant_id, bu_id, owner_id, title, status, completed_at)
            VALUES (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(),
                    gen_random_uuid(), 'Inconsistente', 'completed', NULL);
        ");

        await act.Should().ThrowAsync<PostgresException>()
            .Where(e => e.SqlState == "23514"); // constraint violation
    }

    [Fact]
    public async Task Constraint_Rejects_Pending_Status_With_CompletedAt_Set()
    {
        // Arrange
        await ApplyMigrationDd001Async();

        // Act + Assert
        var act = async () => await ExecuteAsync($@"
            INSERT INTO activities (id, tenant_id, bu_id, owner_id, title, status, completed_at)
            VALUES (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(),
                    gen_random_uuid(), 'Inconsistente', 'pending', '{DateTimeOffset.UtcNow:O}');
        ");

        await act.Should().ThrowAsync<PostgresException>()
            .Where(e => e.SqlState == "23514");
    }

    [Fact]
    public async Task UniqueIndex_Prevents_Duplicate_Token_Hash()
    {
        // Arrange: token_hash é BYTEA (SHA-256, 32 bytes) — ADR-0006, DD-007
        // Usa decode(..., 'hex') para construir BYTEA de 32 bytes diretamente no SQL.
        await ApplyMigrationDd001Async();

        // 32 bytes em hex (64 chars) = hash SHA-256 simulado
        const string hashHex = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        await ExecuteAsync($@"
            INSERT INTO digest_action_tokens (id, tenant_id, user_id, activity_id, token_hash)
            VALUES (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(),
                    gen_random_uuid(), decode('{hashHex}', 'hex'));
        ");

        // Act + Assert: segundo insert com mesmo hash deve falhar (UNIQUE BYTEA)
        var act = async () => await ExecuteAsync($@"
            INSERT INTO digest_action_tokens (id, tenant_id, user_id, activity_id, token_hash)
            VALUES (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(),
                    gen_random_uuid(), decode('{hashHex}', 'hex'));
        ");

        await act.Should().ThrowAsync<PostgresException>()
            .Where(e => e.SqlState == "23505"); // unique violation
    }

    [Fact]
    public async Task Migration_Down_Removes_Status_And_Priority_Columns()
    {
        // Arrange
        await ApplyMigrationDd001Async();

        // Act: Down da migration
        await ExecuteAsync(@"
            ALTER TABLE activities
                DROP CONSTRAINT IF EXISTS chk_activities_completed_consistency,
                DROP CONSTRAINT IF EXISTS chk_activities_priority,
                DROP CONSTRAINT IF EXISTS chk_activities_status,
                DROP COLUMN IF EXISTS priority,
                DROP COLUMN IF EXISTS status;
        ");

        // Assert: colunas não existem mais
        var statusExists = await ScalarAsync(@"
            SELECT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_name = 'activities' AND column_name = 'status')");
        statusExists.Should().Be(false);
    }
}

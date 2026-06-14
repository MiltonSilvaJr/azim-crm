using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using DataMigration.Infrastructure.Persistence;
using Xunit;

namespace DataMigration.Infrastructure.Tests.Persistence;

/// <summary>
/// Fixture Testcontainers que inicializa um contêiner PostgreSQL real para
/// testes de integração de RLS, EF Core e idempotência.
///
/// Implementa <see cref="IAsyncLifetime"/> para que o contêiner seja
/// iniciado/parado automaticamente pelo xUnit.
///
/// Rastreia: TASK-15, ST-01 (RLS falha-fechada, Global Query Filter).
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    // Usuário NOSUPERUSER conforme instrução de Onda 4.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("datamigration_test")
        .WithUsername("datamig_user")
        .WithPassword("datamig_pass")
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Cria as tabelas via migration SQL diretamente.
        await ApplyMigrationsAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Cria um <see cref="MigrationDbContext"/> configurado para o tenant especificado.
    ///
    /// Pooling=false garante que cada conexão passa pelo <see cref="TenantConnectionInterceptor"/>
    /// ao abrir, evitando que o Npgsql reutilize conexões do pool sem re-setar
    /// <c>app.current_tenant</c> (DD-008, RLS falha-fechada).
    /// </summary>
    public MigrationDbContext CreateContext(Guid tenantId)
    {
        var connectionInterceptor = new TenantConnectionInterceptor(tenantId);
        var saveInterceptor = new TenantSaveChangesInterceptor(tenantId);

        // Pooling=false para testes: garante ConnectionOpened a cada uso (RLS segura).
        var noPoolConnStr = ConnectionString + ";Pooling=false";

        var options = new DbContextOptionsBuilder<MigrationDbContext>()
            .UseNpgsql(noPoolConnStr)
            .AddInterceptors(connectionInterceptor, saveInterceptor)
            .Options;

        return new MigrationDbContext(options, tenantId);
    }

    // =========================================================================
    // Aplicação manual das migrations SQL
    // =========================================================================

    private async Task ApplyMigrationsAsync()
    {
        // Usa conexão Npgsql diretamente para aplicar o schema SQL.
        // Evita dependência do EF Core model durante a inicialização do schema.
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        // O superuser do contêiner pode executar DDL sem RLS.
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = GetMigrationSql();
        await cmd.ExecuteNonQueryAsync();
    }

    private static string GetMigrationSql() => """
        CREATE TABLE IF NOT EXISTS migration_jobs (
            id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            tenant_id           UUID NOT NULL,
            status              VARCHAR(20) NOT NULL,
            source_file_name    TEXT NOT NULL,
            source_file_size    BIGINT NOT NULL,
            source_file_hash    TEXT NOT NULL,
            detected_row_count  INTEGER NOT NULL,
            triage_report       JSONB,
            triage_resolution   JSONB,
            import_report       JSONB,
            started_at          TIMESTAMPTZ,
            finished_at         TIMESTAMPTZ,
            created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
            updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
            created_by          UUID NOT NULL,
            CONSTRAINT ck_migration_status CHECK (status IN (
                'created','dry_run_completed','triage_in_progress','ready_to_import',
                'importing','completed','rolled_back','failed'
            ))
        );

        CREATE INDEX IF NOT EXISTS idx_migration_jobs_status
            ON migration_jobs (tenant_id, status);

        CREATE TABLE IF NOT EXISTS migration_logs (
            id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            tenant_id           UUID NOT NULL,
            migration_job_id    UUID NOT NULL REFERENCES migration_jobs(id) ON DELETE CASCADE,
            source_sheet        VARCHAR(40) NOT NULL,
            source_row_index    INTEGER NOT NULL,
            status              VARCHAR(10) NOT NULL,
            message             TEXT NOT NULL,
            import_key          TEXT,
            created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
            CONSTRAINT ck_migration_log_status CHECK (status IN ('ok','aviso','erro'))
        );

        CREATE INDEX IF NOT EXISTS idx_migration_logs_job
            ON migration_logs (tenant_id, migration_job_id, source_row_index);

        CREATE UNIQUE INDEX IF NOT EXISTS uq_migration_log_import_key
            ON migration_logs (tenant_id, import_key)
            WHERE import_key IS NOT NULL;

        ALTER TABLE migration_jobs ENABLE ROW LEVEL SECURITY;
        ALTER TABLE migration_jobs FORCE ROW LEVEL SECURITY;
        CREATE POLICY rls_migration_jobs ON migration_jobs
            USING (tenant_id = current_setting('app.current_tenant')::uuid);

        ALTER TABLE migration_logs ENABLE ROW LEVEL SECURITY;
        ALTER TABLE migration_logs FORCE ROW LEVEL SECURITY;
        CREATE POLICY rls_migration_logs ON migration_logs
            USING (tenant_id = current_setting('app.current_tenant')::uuid);

        CREATE TABLE IF NOT EXISTS outbox_events (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            tenant_id       UUID NOT NULL,
            event_type      VARCHAR(100) NOT NULL,
            payload         JSONB NOT NULL,
            correlation_id  UUID,
            causation_id    UUID,
            created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
            processed_at    TIMESTAMPTZ
        );

        CREATE INDEX IF NOT EXISTS idx_outbox_events_unprocessed
            ON outbox_events (created_at)
            WHERE processed_at IS NULL;

        ALTER TABLE outbox_events ENABLE ROW LEVEL SECURITY;
        ALTER TABLE outbox_events FORCE ROW LEVEL SECURITY;
        CREATE POLICY rls_outbox_events ON outbox_events
            USING (tenant_id = current_setting('app.current_tenant')::uuid);
        """;
}

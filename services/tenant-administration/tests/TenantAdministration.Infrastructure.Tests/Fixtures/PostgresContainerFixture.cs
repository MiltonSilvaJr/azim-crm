using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using TenantAdministration.Application.Ports;
using TenantAdministration.Infrastructure.Identity;
using TenantAdministration.Infrastructure.Outbox;
using TenantAdministration.Infrastructure.Persistence;
using TenantAdministration.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TenantAdministration.Infrastructure.Tests.Fixtures;

/// <summary>
/// Fixture compartilhada que inicia um container PostgreSQL real via Testcontainers.
/// Aplica as migrations do módulo (schema, RLS, triggers) antes dos testes.
/// Usada por TASK-12, 13, 15 e 16 (Testcontainers obrigatórios para RLS — ADR-0001).
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("tenant_administration_test")
        .WithUsername("ta_test")
        .WithPassword("ta_test_pw")
        .Build();

    /// <summary>Connection string do superusuário do container (administração e setup).</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Connection string do usuário de aplicação sem privilégios de superusuário.
    /// Necessário para testes de RLS: PostgreSQL não aplica RLS a superusuários mesmo com FORCE.
    /// </summary>
    public string AppConnectionString { get; private set; } = string.Empty;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        await ApplyMigrationsAsync();

        // Connection string para usuário de aplicação (sem superusuário) — necessário para testes de RLS
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = "ta_app",
            Password = "ta_app_pw"
        };
        AppConnectionString = builder.ConnectionString;
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Cria um DbContext configurado para o tenant informado (com global query filter ativo).
    /// </summary>
    public TenantAdministrationDbContext CreateDbContext(Guid? tenantId = null)
    {
        var options = new DbContextOptionsBuilder<TenantAdministrationDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new TenantAdministrationDbContext(options, tenantId);
    }

    /// <summary>
    /// Helper: aplica SET app.current_tenant em uma conexão aberta com o usuário de aplicação (ta_app).
    /// Obrigatório para testes de RLS — PostgreSQL não aplica RLS a superusuários (ADR-0001).
    /// </summary>
    public async Task<NpgsqlConnection> OpenConnectionWithTenantAsync(Guid tenantId)
    {
        var conn = new NpgsqlConnection(AppConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SET app.current_tenant = '{tenantId:D}'";
        await cmd.ExecuteNonQueryAsync();
        return conn;
    }

    /// <summary>
    /// Helper: abre conexão com o usuário de aplicação (ta_app) sem definir current_tenant.
    /// Usado para testes de isolamento sem contexto de tenant.
    /// </summary>
    public async Task<NpgsqlConnection> OpenAppConnectionAsync()
    {
        var conn = new NpgsqlConnection(AppConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    /// <summary>
    /// Cria helper de repositório para o tenant informado.
    /// </summary>
    public (TenantAdministrationDbContext Db, TenantRepository Repo, FakeTenantContext TenantCtx)
        CreateRepositoryContext(Guid? tenantId = null)
    {
        var ctx = new FakeTenantContext(tenantId);
        var db = CreateDbContext(tenantId);
        var repo = new TenantRepository(db);
        return (db, repo, ctx);
    }

    private async Task ApplyMigrationsAsync()
    {
        // Aplica schema via SQL direto (sem EF Core para evitar problemas de configuração de modelo)
        await ApplyInitialSchemaAsync();
    }

    private async Task ApplyInitialSchemaAsync()
    {
        using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();

        // ── tenants ──────────────────────────────────────────────────────────
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS tenants (
                id                  UUID         NOT NULL DEFAULT gen_random_uuid(),
                slug                TEXT         NOT NULL,
                display_name        TEXT         NOT NULL,
                iana_timezone       TEXT         NOT NULL DEFAULT 'America/Sao_Paulo',
                digest_time         TEXT         NOT NULL DEFAULT '07:00',
                status              VARCHAR(20)  NOT NULL DEFAULT 'provisioned'
                                    CHECK (status IN ('provisioned', 'suspended')),
                active              BOOLEAN      NOT NULL DEFAULT TRUE,
                identity_tenant_id  TEXT         NULL,
                provisioned_at      TIMESTAMPTZ  NOT NULL DEFAULT now(),
                created_at          TIMESTAMPTZ  NOT NULL DEFAULT now(),
                updated_at          TIMESTAMPTZ  NOT NULL DEFAULT now(),
                CONSTRAINT pk_tenants PRIMARY KEY (id)
            );
            CREATE UNIQUE INDEX IF NOT EXISTS uq_tenants_slug ON tenants (slug);
            """;
        await cmd.ExecuteNonQueryAsync();

        cmd.CommandText = """
            CREATE OR REPLACE FUNCTION prevent_slug_update_fn()
            RETURNS TRIGGER LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.slug <> OLD.slug THEN
                    RAISE EXCEPTION 'TA-ERR-SLUG-IMMUTABLE: slug is immutable and cannot be changed';
                END IF;
                RETURN NEW;
            END;
            $$;
            """;
        await cmd.ExecuteNonQueryAsync();

        cmd.CommandText = """
            DROP TRIGGER IF EXISTS prevent_slug_update ON tenants;
            CREATE TRIGGER prevent_slug_update
            BEFORE UPDATE ON tenants
            FOR EACH ROW
            WHEN (OLD.slug IS DISTINCT FROM NEW.slug)
            EXECUTE FUNCTION prevent_slug_update_fn();
            """;
        await cmd.ExecuteNonQueryAsync();

        cmd.CommandText = """
            CREATE OR REPLACE FUNCTION sync_active_from_status_fn()
            RETURNS TRIGGER LANGUAGE plpgsql AS $$
            BEGIN
                NEW.active := (NEW.status = 'provisioned');
                RETURN NEW;
            END;
            $$;
            DROP TRIGGER IF EXISTS sync_active_from_status ON tenants;
            CREATE TRIGGER sync_active_from_status
            BEFORE INSERT OR UPDATE OF status ON tenants
            FOR EACH ROW
            EXECUTE FUNCTION sync_active_from_status_fn();
            """;
        await cmd.ExecuteNonQueryAsync();

        // ── tenant_brandings com RLS ──────────────────────────────────────────
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS tenant_brandings (
                id                  UUID          NOT NULL DEFAULT gen_random_uuid(),
                tenant_id           UUID          NOT NULL,
                logo_url            TEXT          NULL,
                favicon_url         TEXT          NULL,
                primary_color       CHAR(7)       NULL,
                secondary_color     CHAR(7)       NULL,
                wcag_contrast_ok    BOOLEAN       NOT NULL DEFAULT FALSE,
                last_contrast_ratio NUMERIC(4,2)  NULL,
                updated_at          TIMESTAMPTZ   NOT NULL DEFAULT now(),
                CONSTRAINT pk_tenant_brandings PRIMARY KEY (id),
                CONSTRAINT fk_tenant_brandings_tenant_id
                    FOREIGN KEY (tenant_id) REFERENCES tenants (id)
            );
            CREATE UNIQUE INDEX IF NOT EXISTS uq_tenant_brandings_tenant ON tenant_brandings (tenant_id);
            """;
        await cmd.ExecuteNonQueryAsync();

        // RLS — REVISÃO OBRIGATÓRIA (RNF 1.3)
        cmd.CommandText = """
            ALTER TABLE tenant_brandings ENABLE ROW LEVEL SECURITY;
            ALTER TABLE tenant_brandings FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS tenant_isolation_policy ON tenant_brandings;
            CREATE POLICY tenant_isolation_policy ON tenant_brandings
            USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
            """;
        await cmd.ExecuteNonQueryAsync();

        // RLS em tenants
        cmd.CommandText = """
            ALTER TABLE tenants ENABLE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS tenant_self_policy ON tenants;
            CREATE POLICY tenant_self_policy ON tenants
            USING (id = current_setting('app.current_tenant', true)::uuid);
            """;
        await cmd.ExecuteNonQueryAsync();

        // ── outbox_events ─────────────────────────────────────────────────────
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS outbox_events (
                id              UUID         NOT NULL,
                event_type      VARCHAR(100)  NOT NULL,
                aggregate_type  VARCHAR(100)  NOT NULL,
                aggregate_id    UUID         NOT NULL,
                tenant_id       UUID         NULL,
                correlation_id  VARCHAR(200)  NULL,
                payload         JSONB        NOT NULL,
                status          VARCHAR(20)  NOT NULL DEFAULT 'pending'
                                CHECK (status IN ('pending', 'published', 'failed')),
                created_at      TIMESTAMPTZ  NOT NULL DEFAULT now(),
                published_at    TIMESTAMPTZ  NULL,
                retry_count     INT          NOT NULL DEFAULT 0,
                last_error      TEXT         NULL,
                CONSTRAINT pk_outbox_events PRIMARY KEY (id)
            );
            CREATE INDEX IF NOT EXISTS ix_outbox_events_status ON outbox_events (status)
            WHERE status = 'pending';
            """;
        await cmd.ExecuteNonQueryAsync();

        // ── tenant_provisioning_requests ──────────────────────────────────────
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS tenant_provisioning_requests (
                idempotency_key           TEXT        NOT NULL,
                slug                      TEXT        NOT NULL,
                result_tenant_id          UUID        NULL,
                result_identity_tenant_id TEXT        NULL,
                status                    VARCHAR(20) NOT NULL
                                          CHECK (status IN ('in_progress', 'succeeded', 'failed')),
                created_at                TIMESTAMPTZ NOT NULL DEFAULT now(),
                CONSTRAINT pk_tenant_provisioning_requests
                    PRIMARY KEY (idempotency_key)
            );
            """;
        await cmd.ExecuteNonQueryAsync();

        // ── usuário de aplicação (ta_app) para testes de RLS ─────────────────
        // PostgreSQL não aplica RLS a superusuários. ta_app é usuário normal
        // que respeita as policies (ADR-0001, design.md §7.4, RNF 1.3).
        cmd.CommandText = """
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ta_app') THEN
                    CREATE ROLE ta_app LOGIN PASSWORD 'ta_app_pw' NOSUPERUSER NOINHERIT NOCREATEDB NOCREATEROLE;
                END IF;
            END $$;
            GRANT CONNECT ON DATABASE tenant_administration_test TO ta_app;
            GRANT USAGE ON SCHEMA public TO ta_app;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO ta_app;
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO ta_app;
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// Implementação fake de <see cref="ITenantContext"/> para uso em testes.
/// </summary>
public sealed class FakeTenantContext : ITenantContext
{
    /// <param name="tenantId">ID do tenant a simular.</param>
    public FakeTenantContext(Guid? tenantId = null, string? slug = null, string? correlationId = null)
    {
        TenantId = tenantId;
        Slug = slug;
        CorrelationId = correlationId ?? Guid.NewGuid().ToString();
    }

    /// <inheritdoc/>
    public Guid? TenantId { get; }

    /// <inheritdoc/>
    public string? Slug { get; }

    /// <inheritdoc/>
    public string? CorrelationId { get; }
}

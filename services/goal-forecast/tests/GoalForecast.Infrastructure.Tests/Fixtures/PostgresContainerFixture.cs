using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace GoalForecast.Infrastructure.Tests.Fixtures;

/// <summary>
/// Fixture compartilhada que sobe um container PostgreSQL real via Testcontainers
/// e aplica o schema de testes (EF EnsureCreated + SQL de RLS e índices).
///
/// Dois perfis de acesso para testar defesa em profundidade (ADR-0001):
/// <list type="bullet">
///   <item><term>owner (azim_test)</term><description>Superuser/owner; bypassa RLS para inserção de dados de teste via owner context.</description></item>
///   <item><term>app (azim_app, NOSUPERUSER)</term><description>Usuário de aplicação; sujeito à RLS. Usado em testes de isolamento (TASK-18, KPI-06).</description></item>
/// </list>
///
/// Estratégia de uso:
/// <list type="bullet">
///   <item><term>BuildOwnerContext</term><description>Inserção de dados de fixture (bypassa RLS do owner).</description></item>
///   <item><term>BuildContextWithRls</term><description>Testes de aplicação (app_user + app.tenant_id via interceptor EF Core).</description></item>
///   <item><term>OpenAppConnection</term><description>Testes de SQL direto (RLS bruta).</description></item>
/// </list>
///
/// Mapeia: TASK-15..20, design §6.1, §7, ADR-0001.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private const string AppUser = "azim_app";
    private const string AppPassword = "azim_app_pwd";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithUsername("azim_test")
        .WithPassword("azim_test_pwd")
        .WithDatabase("goal_forecast_test")
        .Build();

    /// <summary>String de conexão do owner (azim_test) para setup do schema.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>String de conexão do app_user (azim_app, NOSUPERUSER) sujeito à RLS.</summary>
    public string AppConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Cria schema via EnsureCreated (owner) + SQL de RLS, índices e app_user
        using var ctx = BuildOwnerContext(Guid.NewGuid());
        await ctx.Database.EnsureCreatedAsync();
        await ApplyRlsIndexesAndAppUserAsync();

        var builder = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = AppUser,
            Password = AppPassword
        };
        AppConnectionString = builder.ConnectionString;
    }

    private async Task ApplyRlsIndexesAndAppUserAsync()
    {
        await using var conn = OpenOwnerConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = $"""
            CREATE UNIQUE INDEX IF NOT EXISTS ux_goals_responsavel_scope
                ON goals (tenant_id, bu_id, owner_id, year, month)
                WHERE owner_id IS NOT NULL;

            CREATE UNIQUE INDEX IF NOT EXISTS ux_goals_bu_scope
                ON goals (tenant_id, bu_id, year, month)
                WHERE owner_id IS NULL;

            CREATE INDEX IF NOT EXISTS ix_goals_tenant_bu_period
                ON goals (tenant_id, bu_id, year, month);

            CREATE INDEX IF NOT EXISTS ix_goals_tenant_owner_period
                ON goals (tenant_id, owner_id, year, month);

            CREATE TABLE IF NOT EXISTS outbox_events (
                id              UUID        NOT NULL DEFAULT gen_random_uuid(),
                event_id        UUID        NOT NULL,
                tenant_id       UUID        NOT NULL,
                event_type      TEXT        NOT NULL,
                payload         JSONB       NOT NULL,
                created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
                processed_at    TIMESTAMPTZ,
                CONSTRAINT pk_outbox_events PRIMARY KEY (id),
                CONSTRAINT uq_outbox_event_id UNIQUE (event_id)
            );

            CREATE INDEX IF NOT EXISTS ix_outbox_events_unprocessed
                ON outbox_events (created_at)
                WHERE processed_at IS NULL;

            ALTER TABLE goals ENABLE ROW LEVEL SECURITY;
            ALTER TABLE goals FORCE ROW LEVEL SECURITY;

            DROP POLICY IF EXISTS goals_tenant_isolation ON goals;
            CREATE POLICY goals_tenant_isolation ON goals
                USING (
                    tenant_id = CASE
                        WHEN current_setting('app.tenant_id', true) = '' OR
                             current_setting('app.tenant_id', true) IS NULL
                        THEN '00000000-0000-0000-0000-000000000000'::uuid
                        ELSE current_setting('app.tenant_id', true)::uuid
                    END
                )
                WITH CHECK (
                    tenant_id = CASE
                        WHEN current_setting('app.tenant_id', true) = '' OR
                             current_setting('app.tenant_id', true) IS NULL
                        THEN '00000000-0000-0000-0000-000000000000'::uuid
                        ELSE current_setting('app.tenant_id', true)::uuid
                    END
                );

            -- Usuário de aplicação NOSUPERUSER para testar RLS sem ser owner (TASK-18)
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = '{AppUser}') THEN
                    CREATE ROLE {AppUser} WITH LOGIN PASSWORD '{AppPassword}' NOSUPERUSER NOCREATEDB NOCREATEROLE;
                END IF;
            END $$;

            GRANT CONNECT ON DATABASE goal_forecast_test TO {AppUser};
            GRANT USAGE ON SCHEMA public TO {AppUser};
            GRANT SELECT, INSERT, UPDATE, DELETE ON goals TO {AppUser};
            GRANT SELECT, INSERT ON outbox_events TO {AppUser};
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    // ── Owner context (usado para inserção de dados de teste) ────────────────

    /// <summary>
    /// Cria DbContext EF Core com o owner (azim_test). O owner bypassa FORCE RLS
    /// como comportamento padrão do PostgreSQL (owner da tabela não sofre RLS).
    /// Usar para inserções de dados de fixture em testes de GoalRepository e DbContextMapping.
    /// </summary>
    public GoalForecastDbContext BuildOwnerContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<GoalForecastDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new GoalForecastDbContext(options, tenantId);
    }

    // ── App context (sujeito à RLS — usado em testes de aplicação) ───────────

    /// <summary>
    /// Cria DbContext EF Core com o usuário de aplicação (azim_app, NOSUPERUSER),
    /// sujeito à RLS. O parâmetro <c>app.tenant_id</c> é embutido na connection string
    /// via interceptor de sessão de conexão para garantir aplicação em cada conexão aberta.
    /// </summary>
    public GoalForecastDbContext BuildContextWithRls(Guid tenantId)
    {
        // Usa NpgsqlDataSource com um callback que executa SET app.tenant_id
        // antes de entregar a conexão ao pool EF Core. Isso garante que cada
        // conexão nova (inclusive reconexões) terá app.tenant_id configurado.
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(AppConnectionString);
        dataSourceBuilder.UsePhysicalConnectionInitializer(
            conn =>
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"SET app.tenant_id = '{tenantId}'";
                cmd.ExecuteNonQuery();
            },
            async conn =>
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"SET app.tenant_id = '{tenantId}'";
                await cmd.ExecuteNonQueryAsync();
            });

        var dataSource = dataSourceBuilder.Build();

        var options = new DbContextOptionsBuilder<GoalForecastDbContext>()
            .UseNpgsql(dataSource)
            .Options;

        return new GoalForecastDbContext(options, tenantId);
    }

    // ── Conexões raw ──────────────────────────────────────────────────────────

    /// <summary>
    /// Abre conexão raw com o owner (azim_test).
    /// Usado em setup do schema e em inserções de dados de teste sem RLS.
    /// </summary>
    public NpgsqlConnection OpenOwnerConnection()
    {
        var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    /// <summary>
    /// Abre conexão raw com o usuário de aplicação (azim_app, NOSUPERUSER), sem SET app.tenant_id.
    /// Usado nos testes de isolamento RLS para verificar que RLS retorna 0 linhas
    /// quando não há contexto de tenant (segunda camada ADR-0001, TASK-18, KPI-06).
    /// </summary>
    public NpgsqlConnection OpenAppConnection()
    {
        var conn = new NpgsqlConnection(AppConnectionString);
        conn.Open();
        return conn;
    }
}

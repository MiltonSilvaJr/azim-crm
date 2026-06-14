using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace GoalForecast.Infrastructure.Tests.Fixtures;

/// <summary>
/// Fixture compartilhada que sobe um container PostgreSQL real via Testcontainers
/// e aplica as migrations do EF Core. Compartilhada na collection para evitar
/// múltiplos containers por classe de teste.
///
/// Mapeia: TASK-15..20, design §6.1, §7.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithUsername("azim_test")
        .WithPassword("azim_test_pwd")
        .WithDatabase("goal_forecast_test")
        .Build();

    /// <summary>String de conexão para criar contextos EF Core nos testes.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Cria o schema via EnsureCreated (do EF model) + SQL adicional para RLS.
        // EnsureCreated usa o modelo EF para criar as tabelas sem precisar de migrations
        // registradas no assembly. O SQL de RLS e índices parciais é aplicado manualmente.
        using var ctx = BuildContext(Guid.NewGuid());
        await ctx.Database.EnsureCreatedAsync();
        await ApplyRlsAndIndexesAsync();
    }

    private async Task ApplyRlsAndIndexesAsync()
    {
        using var conn = OpenRawConnection();
        using var cmd = conn.CreateCommand();

        // Índice único parcial para escopo RESPONSAVEL (DD-002)
        cmd.CommandText = """
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
                );
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Cria um DbContext EF Core com o tenantId informado.
    /// Global Query Filter usa este tenantId em todas as queries.
    /// </summary>
    public GoalForecastDbContext BuildContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<GoalForecastDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new GoalForecastDbContext(options, tenantId);
    }

    /// <summary>
    /// Abre uma conexão Npgsql raw sem passar pelo DbContext / EF Core.
    /// Usado nos testes de isolamento RLS (TASK-18) para exercitar a segunda camada.
    /// </summary>
    public NpgsqlConnection OpenRawConnection()
    {
        var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    /// <summary>
    /// Cria um DbContext com o tenantId configurado como parâmetro de sessão
    /// (SET app.tenant_id = '...') para que a RLS permita o acesso.
    /// </summary>
    public GoalForecastDbContext BuildContextWithRls(Guid tenantId)
    {
        var ctx = BuildContext(tenantId);
        // Configura o parâmetro de sessão para que a policy RLS libere as linhas
        ctx.Database.ExecuteSql($"SET app.tenant_id = '{tenantId}'");
        return ctx;
    }
}

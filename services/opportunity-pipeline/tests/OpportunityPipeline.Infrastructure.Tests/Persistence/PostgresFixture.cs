using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Infrastructure.Persistence;
using OpportunityPipeline.Infrastructure.Tenancy;
using Testcontainers.PostgreSql;

namespace OpportunityPipeline.Infrastructure.Tests.Persistence;

/// <summary>
/// Fixture de PostgreSQL real via Testcontainers.
/// Compartilhada entre testes de Onda 4 (collection fixture).
/// Cria o schema com o superusuário (testapp) e aplica RLS.
/// Expõe AppConnectionString para conexões de teste como appuser (NOSUPERUSER),
/// garantindo que RLS seja efetivamente aplicado — superusuários bypassam RLS.
/// Mapeia: design §6.1, ADR-0001, TASK-13.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Usuário administrativo — cria schema e grant (POSTGRES_USER = superuser no container oficial).
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithUsername("testapp")
        .WithPassword("testpass")
        .WithDatabase("opportunity_test")
        .Build();

    // String de conexão de schema/admin (superuser — usada apenas para setup)
    public string ConnectionString => _container.GetConnectionString();

    // String de conexão para a aplicação (NOSUPERUSER appuser — sujeito ao RLS)
    public string AppConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await ApplySchemaAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Cria um DbContext para o tenant informado conectado como appuser (NOSUPERUSER).
    /// O RlsConnectionInterceptor seta app.current_tenant em cada abertura de conexão.
    /// Usar AppConnectionString garante que RLS é efetivamente avaliado (ADR-0001 camada 3).
    /// </summary>
    public async Task<OpportunityDbContext> CreateContextAsync(Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.Initialize(tenantId, Guid.NewGuid(), Guid.NewGuid());

        var interceptor = new RlsConnectionInterceptor(
            tenantContext,
            NullLogger<RlsConnectionInterceptor>.Instance);

        var options = new DbContextOptionsBuilder<OpportunityDbContext>()
            .UseNpgsql(AppConnectionString)
            .AddInterceptors(interceptor)
            .Options;

        var context = new OpportunityDbContext(options, tenantContext);
        return await Task.FromResult(context);
    }

    private async Task ApplySchemaAsync()
    {
        // Usa NpgsqlConnection diretamente para evitar que ExecuteSqlRawAsync
        // interprete chaves {} do SQL (JSONB defaults, PL/pgSQL) como String.Format placeholders.
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        // =====================================================================
        // Schema principal (tabelas, índices, constraints)
        // =====================================================================
        await ExecuteNonQueryAsync(connection, @"
CREATE TABLE IF NOT EXISTS opportunities (
    id                          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id                   UUID         NOT NULL,
    bu_id                       UUID         NOT NULL,
    account_id                  UUID         NOT NULL,
    partner_id                  UUID,
    stage_id                    UUID         NOT NULL,
    stage_name                  VARCHAR(100) NOT NULL DEFAULT '',
    stage_category_ref          VARCHAR(20)  NOT NULL DEFAULT 'open',
    stage_default_probability   SMALLINT     NOT NULL DEFAULT 0,
    stage_order                 INTEGER      NOT NULL DEFAULT 0,
    owner_id                    UUID         NOT NULL,
    origin_channel_id           UUID         NOT NULL,
    origin_channel_name         VARCHAR(100) NOT NULL DEFAULT '',
    origin_is_partner_channel   BOOLEAN      NOT NULL DEFAULT FALSE,
    opportunity_number          VARCHAR(10)  NOT NULL,
    title                       TEXT         NOT NULL,
    valor_setup                 BIGINT       NOT NULL DEFAULT 0,
    valor_mensal                BIGINT       NOT NULL DEFAULT 0,
    duracao_meses               INTEGER      NOT NULL DEFAULT 0,
    probabilidade               SMALLINT     NOT NULL DEFAULT 0,
    data_fechamento_esperada    DATE,
    loss_reason_id              UUID,
    loss_reason_name            VARCHAR(200),
    closed_at                   TIMESTAMPTZ,
    stage_category              VARCHAR(20)  NOT NULL DEFAULT 'open',
    is_stale                    BOOLEAN      NOT NULL DEFAULT FALSE,
    notes                       TEXT,
    created_at                  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_at                  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    created_by                  UUID         NOT NULL,
    CONSTRAINT uq_opportunities_tenant_number UNIQUE (tenant_id, opportunity_number),
    CONSTRAINT chk_opportunities_category CHECK (stage_category IN ('open','won','lost')),
    CONSTRAINT chk_opportunities_prob CHECK (probabilidade BETWEEN 0 AND 100),
    CONSTRAINT chk_opportunities_values_nonneg CHECK (valor_setup >= 0 AND valor_mensal >= 0 AND duracao_meses >= 0)
)");

        await ExecuteNonQueryAsync(connection, @"
CREATE TABLE IF NOT EXISTS opportunity_stage_transitions (
    id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID        NOT NULL,
    opportunity_id  UUID        NOT NULL REFERENCES opportunities(id),
    from_stage_id   UUID,
    to_stage_id     UUID        NOT NULL,
    from_category   VARCHAR(20),
    to_category     VARCHAR(20) NOT NULL,
    actor_id        UUID        NOT NULL,
    occurred_at     TIMESTAMPTZ NOT NULL DEFAULT now()
)");

        await ExecuteNonQueryAsync(connection, @"
CREATE TABLE IF NOT EXISTS opportunity_partner_commissions (
    id                        UUID           PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id                 UUID           NOT NULL,
    opportunity_id            UUID           NOT NULL REFERENCES opportunities(id),
    partner_id                UUID           NOT NULL,
    role                      VARCHAR(20)    NOT NULL DEFAULT 'Indicador',
    pct_setup                 NUMERIC(5,2)   NOT NULL DEFAULT 0,
    pct_recorrente            NUMERIC(5,2)   NOT NULL DEFAULT 0,
    valor_fixo                BIGINT         NOT NULL DEFAULT 0,
    meses_comissionados       INTEGER        NOT NULL DEFAULT 0,
    comissao_setup_cents      BIGINT         NOT NULL DEFAULT 0,
    comissao_recorrente_cents BIGINT         NOT NULL DEFAULT 0,
    comissao_calculada        BIGINT         NOT NULL DEFAULT 0,
    is_snapshot               BOOLEAN        NOT NULL DEFAULT FALSE,
    snapshot_at               TIMESTAMPTZ,
    created_at                TIMESTAMPTZ    NOT NULL DEFAULT now()
)");

        await ExecuteNonQueryAsync(connection, @"
CREATE UNIQUE INDEX IF NOT EXISTS uq_partner_commission_active_snapshot
    ON opportunity_partner_commissions (opportunity_id)
    WHERE is_snapshot = TRUE");

        await ExecuteNonQueryAsync(connection, @"
CREATE TABLE IF NOT EXISTS opportunity_contacts (
    id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID        NOT NULL,
    opportunity_id  UUID        NOT NULL REFERENCES opportunities(id),
    contact_id      UUID        NOT NULL,
    is_primary      BOOLEAN     NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_opportunity_contacts_link UNIQUE (opportunity_id, contact_id)
)");

        await ExecuteNonQueryAsync(connection, @"
CREATE TABLE IF NOT EXISTS opportunity_number_sequences (
    tenant_id   UUID   PRIMARY KEY,
    next_value  BIGINT NOT NULL DEFAULT 1
)");

        await ExecuteNonQueryAsync(connection, @"
CREATE TABLE IF NOT EXISTS stale_detection_runs (
    tenant_id           UUID        NOT NULL,
    opportunity_id      UUID        NOT NULL,
    detection_period    VARCHAR(10) NOT NULL,
    detected_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (tenant_id, opportunity_id, detection_period)
)");

        await ExecuteNonQueryAsync(connection, @"
CREATE TABLE IF NOT EXISTS saved_filters (
    id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   UUID        NOT NULL,
    user_id     UUID        NOT NULL,
    name        TEXT        NOT NULL,
    criteria    JSONB       NOT NULL DEFAULT '{}',
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
)");

        await ExecuteNonQueryAsync(connection, @"
CREATE TABLE IF NOT EXISTS outbox_events (
    id           UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id    UUID         NOT NULL,
    event_type   VARCHAR(100) NOT NULL,
    payload      JSONB        NOT NULL DEFAULT '{}',
    status       VARCHAR(20)  NOT NULL DEFAULT 'pending',
    created_at   TIMESTAMPTZ  NOT NULL DEFAULT now(),
    published_at TIMESTAMPTZ,
    attempts     INTEGER      NOT NULL DEFAULT 0,
    last_error   TEXT
)");

        // =====================================================================
        // Trigger de imutabilidade de snapshot (TASK-14, DD-002)
        // =====================================================================
        await ExecuteNonQueryAsync(connection, @"
CREATE OR REPLACE FUNCTION block_snapshot_mutation()
RETURNS trigger AS $$
BEGIN
    IF TG_OP = 'DELETE' AND OLD.is_snapshot THEN
        RAISE EXCEPTION 'commission snapshot is immutable (RN-007/RN-022) — DELETE negado';
    END IF;
    IF TG_OP = 'UPDATE' AND OLD.is_snapshot THEN
        RAISE EXCEPTION 'commission snapshot is immutable (RN-007/RN-022) — UPDATE negado';
    END IF;
    RETURN COALESCE(NEW, OLD);
END;
$$ LANGUAGE plpgsql");

        await ExecuteNonQueryAsync(connection, @"
DROP TRIGGER IF EXISTS trg_block_snapshot_mutation ON opportunity_partner_commissions");

        await ExecuteNonQueryAsync(connection, @"
CREATE TRIGGER trg_block_snapshot_mutation
    BEFORE UPDATE OR DELETE ON opportunity_partner_commissions
    FOR EACH ROW
    EXECUTE FUNCTION block_snapshot_mutation()");

        // =====================================================================
        // Usuário NOSUPERUSER para conexões de aplicação (ADR-0001 camada 3).
        // O usuário testapp (criado via POSTGRES_USER no container) é superusuário
        // e bypassa RLS. O appuser é NOSUPERUSER e está sujeito às RLS policies.
        // =====================================================================
        await ExecuteNonQueryAsync(connection, @"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'appuser') THEN
        CREATE ROLE appuser WITH LOGIN PASSWORD 'apppass' NOSUPERUSER NOCREATEDB NOCREATEROLE;
    END IF;
END;
$$");

        // Grant de acesso a todas as tabelas e sequences para appuser
        await ExecuteNonQueryAsync(connection, @"
GRANT CONNECT ON DATABASE opportunity_test TO appuser");

        await ExecuteNonQueryAsync(connection, @"
GRANT USAGE ON SCHEMA public TO appuser");

        await ExecuteNonQueryAsync(connection, @"
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO appuser");

        await ExecuteNonQueryAsync(connection, @"
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO appuser");

        // =====================================================================
        // RLS em todas as 7 tabelas operacionais + outbox (ADR-0001 camada 3)
        // =====================================================================
        foreach (var (table, policy) in new[]
        {
            ("opportunities",                    "opp_tenant_isolation"),
            ("opportunity_stage_transitions",    "transitions_tenant_isolation"),
            ("opportunity_partner_commissions",  "commissions_tenant_isolation"),
            ("opportunity_contacts",             "contacts_tenant_isolation"),
            ("opportunity_number_sequences",     "sequences_tenant_isolation"),
            ("stale_detection_runs",             "stale_runs_tenant_isolation"),
            ("saved_filters",                    "saved_filters_tenant_isolation"),
            ("outbox_events",                    "outbox_tenant_isolation"),
        })
        {
            // FOR ALL: aplica USING para SELECT/UPDATE/DELETE e WITH CHECK para INSERT/UPDATE.
            // Necessário com FORCE ROW LEVEL SECURITY para que INSERT também satisfaça a policy.
            // NULLIF(..., '') converte string vazia para NULL, evitando erro de cast uuid inválido
            // quando app.current_tenant é reset para '' (default de GUC customizado sem valor padrão).
            await ExecuteNonQueryAsync(connection, $@"
ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;
ALTER TABLE {table} FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS {policy} ON {table};
CREATE POLICY {policy} ON {table}
    FOR ALL
    USING (tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::UUID)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::UUID)");
        }

        // Constrói a AppConnectionString substituindo o usuário/senha pelo appuser
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = "appuser",
            Password = "apppass"
        };
        AppConnectionString = builder.ConnectionString;
    }

    private static async Task ExecuteNonQueryAsync(NpgsqlConnection connection, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// Coleção xUnit para compartilhar o PostgresFixture entre todos os testes de Infrastructure.
/// DisableTestParallelization: garante execução sequencial dentro da collection —
/// necessário para evitar race conditions com Testcontainers (múltiplos contextos EF,
/// app.current_tenant, e operações de schema).
/// </summary>
[CollectionDefinition("PostgresCollection")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

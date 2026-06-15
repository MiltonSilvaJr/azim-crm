using AccountManagement.Application.Behaviors;
using AccountManagement.Infrastructure.Persistence;
using AccountManagement.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace AccountManagement.Infrastructure.Tests.Persistence;

/// <summary>
/// Fixture compartilhada que sobe um container PostgreSQL real via Testcontainers.
///
/// Arquitetura de isolamento (ADR-0001, ADR-0009):
///
/// O usuário bootstrap (<c>testapp</c>) é superusuário — necessário para aplicar migrations,
/// criar roles e conceder grants. Superusuários bypassam RLS em PostgreSQL, portanto
/// <b>não devem ser usados em conexões de aplicação</b>.
///
/// Por isso, o fixture cria a role <c>appuser</c> (NOSUPERUSER) e expõe
/// <see cref="AppConnectionString"/>, que conecta como <c>appuser</c>. Todas as queries
/// de aplicação (<see cref="CreateDbContext"/>, <see cref="CreateDbContextWithBuScope"/>)
/// usam <see cref="AppConnectionString"/> com o <see cref="TenantBuScopeConnectionInterceptor"/>
/// para setar <c>app.current_tenant</c>, <c>app.current_bu_scope</c> e <c>app.bu_tenant_wide</c>
/// antes de cada query — submetendo as queries às RLS policies reais de <c>accounts</c> e
/// <c>contacts</c> (não só ao EF Global Query Filter).
///
/// O seed de dados cross-tenant/cross-BU usa a conexão admin (<see cref="ConnectionString"/>
/// ou <see cref="CreateDbContextAdmin"/>) para inserir dados que bypassam RLS.
///
/// RLS aplicada:
/// - <c>accounts</c>: policy <c>bu_scope_isolation</c> (tenant + BU, com <c>FORCE</c>) —
///   da migration <c>20260615000003_AddBuIdToAccounts</c> (ADR-0009).
/// - <c>contacts</c>: policy <c>tenant_isolation</c> (tenant apenas, com <c>FORCE</c>) —
///   da migration <c>20260615000003_AddBuIdToAccounts</c>.
/// - <c>audit_logs</c>, <c>outbox_messages</c>, <c>idempotency_keys</c>: RLS não forçada
///   (tabelas operacionais sem isolamento por escopo de usuário nos testes).
///
/// Mapeia: TASK-08 (ST-01), TASK-10 (ST-01), ADR-0001, ADR-0009, VAL-ACC-03.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Usuário bootstrap — superusuário (POSTGRES_USER no container PostgreSQL oficial).
    // Cria schema, migrations, roles e grants. Bypassa RLS — não usar em queries de aplicação.
    private readonly PostgreSqlContainer _postgres;

    /// <summary>
    /// String de conexão de administração (superusuário).
    /// Usar apenas em: setup de schema, seed de dados cross-tenant e verificações diretas via SQL.
    /// Queries de aplicação devem usar <see cref="AppConnectionString"/>.
    /// </summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// String de conexão como <c>appuser</c> (NOSUPERUSER).
    /// Sujeita às RLS policies de <c>accounts</c> e <c>contacts</c>.
    /// Usar em: <see cref="CreateDbContext"/>, <see cref="CreateDbContextWithBuScope"/>.
    /// </summary>
    public string AppConnectionString { get; private set; } = string.Empty;

    public PostgresFixture()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("account_management_test")
            .WithUsername("testapp")
            .WithPassword("testpass")
            .Build();
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        ConnectionString = _postgres.GetConnectionString();

        // Aplica migrations reais (que incluem as RLS policies de produção)
        var adminOptions = CreateAdminDbContextOptions();
        await using var context = new AccountManagementDbContext(adminOptions);

        var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
        var applied = (await context.Database.GetAppliedMigrationsAsync()).ToList();

        if (pending.Count > 0)
        {
            await context.Database.MigrateAsync();
        }
        else if (applied.Count == 0)
        {
            // Fallback: sem migrations detectadas — cria schema e aplica SQLs customizados
            await context.Database.EnsureCreatedAsync();
            await ApplyCustomMigrationSqlAsync();
        }

        // Cria appuser NOSUPERUSER, concede grants e constrói AppConnectionString
        await SetupAppUserAndRlsAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    // =========================================================================
    // Fábricas de DbContext — APLICAÇÃO (appuser, sujeito a RLS)
    // =========================================================================

    /// <summary>
    /// Cria um DbContext conectado como <c>appuser</c> com visão tenant-wide de BU.
    /// O <see cref="TenantBuScopeConnectionInterceptor"/> seta <c>app.current_tenant</c> e
    /// <c>app.bu_tenant_wide = 'true'</c> na abertura da conexão — submetendo a query à RLS.
    /// </summary>
    public AccountManagementDbContext CreateDbContext(Guid tenantId)
    {
        var tenantCtx = BuildTenantContext(tenantId);
        var buScopeCtx = BuildBuScopeContext(Array.Empty<Guid>(), isTenantWide: true);
        return CreateAppDbContext(tenantCtx, buScopeCtx);
    }

    /// <summary>
    /// Cria um DbContext conectado como <c>appuser</c> com escopo de BU explícito.
    /// Fail-closed: escopo vazio + não-tenant-wide ⇒ zero linhas (EF + RLS).
    /// </summary>
    public AccountManagementDbContext CreateDbContextWithBuScope(
        Guid tenantId,
        IReadOnlyCollection<Guid> buIds,
        bool isTenantWide = false)
    {
        var tenantCtx = BuildTenantContext(tenantId);
        var buScopeCtx = BuildBuScopeContext(buIds, isTenantWide);
        return CreateAppDbContext(tenantCtx, buScopeCtx);
    }

    // =========================================================================
    // Fábricas de DbContext — ADMIN (superusuário, bypassa RLS)
    // =========================================================================

    /// <summary>
    /// Cria um DbContext sem filtros de tenant/BU, conectado como superusuário.
    /// Usar exclusivamente para seed de dados de teste (inserções cross-tenant/cross-BU).
    /// Bypassa RLS — não usar em asserções de isolamento.
    /// </summary>
    public AccountManagementDbContext CreateDbContextAdmin()
    {
        return new AccountManagementDbContext(CreateAdminDbContextOptions());
    }

    /// <summary>
    /// Alias de <see cref="CreateDbContextAdmin"/> para compatibilidade com testes existentes.
    /// </summary>
    public AccountManagementDbContext CreateDbContextNoFilter() => CreateDbContextAdmin();

    // =========================================================================
    // Implementação interna
    // =========================================================================

    private AccountManagementDbContext CreateAppDbContext(
        InfrastructureTenantContext tenantCtx,
        InfrastructureBuScopeContext buScopeCtx)
    {
        var interceptor = new TenantBuScopeConnectionInterceptor(tenantCtx, buScopeCtx);

        var options = new DbContextOptionsBuilder<AccountManagementDbContext>()
            .UseNpgsql(AppConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .AddInterceptors(interceptor)
            .Options;

        return new AccountManagementDbContext(options, tenantCtx, buScopeCtx);
    }

    private DbContextOptions<AccountManagementDbContext> CreateAdminDbContextOptions()
    {
        return new DbContextOptionsBuilder<AccountManagementDbContext>()
            .UseNpgsql(ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .Options;
    }

    private static InfrastructureTenantContext BuildTenantContext(Guid tenantId)
    {
        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(tenantId);
        return new InfrastructureTenantContext(tenantCtx);
    }

    private static InfrastructureBuScopeContext BuildBuScopeContext(
        IReadOnlyCollection<Guid> buIds,
        bool isTenantWide)
    {
        var buScopeCtx = new BuScopeContext();
        buScopeCtx.SetScope(buIds, isTenantWide);
        return new InfrastructureBuScopeContext(buScopeCtx);
    }

    /// <summary>
    /// Cria a role <c>appuser</c> (NOSUPERUSER), concede grants nas tabelas e sequences,
    /// e constrói <see cref="AppConnectionString"/>.
    ///
    /// Nota: as RLS policies de <c>accounts</c> e <c>contacts</c> já foram aplicadas
    /// pelas migrations (migration 20260615000003_AddBuIdToAccounts). Este método apenas
    /// prepara o usuário de aplicação que ficará sujeito a essas policies.
    /// </summary>
    private async Task SetupAppUserAndRlsAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        // Cria appuser NOSUPERUSER (idempotente)
        await ExecuteNonQueryAsync(connection, @"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'appuser') THEN
        CREATE ROLE appuser WITH LOGIN PASSWORD 'apppass' NOSUPERUSER NOCREATEDB NOCREATEROLE;
    END IF;
END;
$$");

        // Grants de conexão e schema
        await ExecuteNonQueryAsync(connection, @"
GRANT CONNECT ON DATABASE account_management_test TO appuser");

        await ExecuteNonQueryAsync(connection, @"
GRANT USAGE ON SCHEMA public TO appuser");

        // Grants nas tabelas (inclui audit_logs — appuser precisa de INSERT para AuditPublisher)
        await ExecuteNonQueryAsync(connection, @"
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO appuser");

        // Grants em sequences
        await ExecuteNonQueryAsync(connection, @"
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO appuser");

        // Revoga UPDATE/DELETE/TRUNCATE em audit_logs para appuser (RNF 8.3 — menor privilégio)
        await ExecuteNonQueryAsync(connection, @"
REVOKE UPDATE, DELETE, TRUNCATE ON audit_logs FROM appuser");

        // Garante que as RLS policies de accounts e contacts estejam aplicadas.
        // As migrations já criam as policies — este bloco garante consistência mesmo no
        // fallback de EnsureCreated (caso as migrations não tenham sido detectadas).
        await EnsureRlsPoliciesAsync(connection);

        // Constrói AppConnectionString
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = "appuser",
            Password = "apppass"
        };
        AppConnectionString = builder.ConnectionString;
    }

    /// <summary>
    /// Garante que as RLS policies de <c>accounts</c> e <c>contacts</c> estejam ativas.
    ///
    /// Em ambiente com migrations reais aplicadas, as policies já existem. Este método
    /// é idempotente (DROP IF EXISTS + CREATE) e serve como safety net para o caminho
    /// de fallback via EnsureCreated, que não aplica SQL customizado das migrations.
    ///
    /// As policies replicam exatamente o que a migration 20260615000003 aplica em produção.
    /// </summary>
    private static async Task EnsureRlsPoliciesAsync(NpgsqlConnection connection)
    {
        // Função auxiliar para conversão de app.current_bu_scope → uuid[]
        await ExecuteNonQueryAsync(connection, @"
CREATE OR REPLACE FUNCTION current_bu_scope_array()
RETURNS uuid[] AS $$
DECLARE
    raw_scope TEXT;
BEGIN
    raw_scope := current_setting('app.current_bu_scope', TRUE);
    IF raw_scope IS NULL OR raw_scope = '' THEN
        RETURN ARRAY[]::uuid[];
    END IF;
    RETURN string_to_array(raw_scope, ',')::uuid[];
END;
$$ LANGUAGE plpgsql STABLE SECURITY DEFINER");

        // =====================================================================
        // accounts: RLS com restrição de tenant + BU (ADR-0009)
        // Fail-closed: sem tenant ou sem BU scope ⇒ zero linhas.
        // =====================================================================
        await ExecuteNonQueryAsync(connection, @"
ALTER TABLE accounts ENABLE ROW LEVEL SECURITY;
ALTER TABLE accounts FORCE ROW LEVEL SECURITY");

        await ExecuteNonQueryAsync(connection, @"
DROP POLICY IF EXISTS tenant_isolation ON accounts;
DROP POLICY IF EXISTS bu_scope_isolation ON accounts;

CREATE POLICY bu_scope_isolation ON accounts
    AS PERMISSIVE
    FOR ALL
    USING (
        tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::uuid
        AND (
            current_setting('app.bu_tenant_wide', TRUE) = 'true'
            OR bu_id = ANY(current_bu_scope_array())
        )
    )");

        // =====================================================================
        // contacts: RLS com restrição de tenant apenas (ADR-0001)
        // BU é herdada via FK para accounts — não duplicar lógica de BU aqui.
        // =====================================================================
        await ExecuteNonQueryAsync(connection, @"
ALTER TABLE contacts ENABLE ROW LEVEL SECURITY;
ALTER TABLE contacts FORCE ROW LEVEL SECURITY");

        await ExecuteNonQueryAsync(connection, @"
DROP POLICY IF EXISTS tenant_isolation ON contacts;

CREATE POLICY tenant_isolation ON contacts
    AS PERMISSIVE
    FOR ALL
    USING (
        tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::uuid
    )");
    }

    /// <summary>
    /// Aplica SQLs customizados das migrations quando o schema foi criado via EnsureCreated
    /// (fallback quando migrations não são detectadas pelo EF Core).
    /// </summary>
    private async Task ApplyCustomMigrationSqlAsync()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        // Função PL/pgSQL de imutabilidade de audit_logs
        await ExecuteNonQueryAsync(conn, @"
CREATE OR REPLACE FUNCTION prevent_immutable_table_modification()
RETURNS trigger AS $$
BEGIN
    RAISE EXCEPTION 'Modificação proibida: audit_logs é append-only (RNF 8). Operação: %', TG_OP
        USING ERRCODE = 'restrict_violation',
              DETAIL = 'audit_logs não admite UPDATE, DELETE ou TRUNCATE.',
              HINT = 'Use apenas INSERT para registrar eventos de auditoria.';
    RETURN NULL;
END;
$$ LANGUAGE plpgsql");

        // Tabela audit_logs (caso EnsureCreated não a tenha criado)
        await ExecuteNonQueryAsync(conn, @"
CREATE TABLE IF NOT EXISTS audit_logs (
    id          UUID NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    tenant_id   UUID NOT NULL,
    user_id     UUID NOT NULL,
    entity_type TEXT NOT NULL,
    entity_id   UUID NOT NULL,
    action      TEXT NOT NULL,
    delta_json  JSONB NOT NULL DEFAULT '{}',
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
)");

        await ExecuteNonQueryAsync(conn, @"
CREATE INDEX IF NOT EXISTS idx_audit_logs_tenant_entity
    ON audit_logs (tenant_id, entity_type, entity_id)");

        await ExecuteNonQueryAsync(conn, @"
DROP TRIGGER IF EXISTS trg_audit_logs_immutable ON audit_logs;
CREATE TRIGGER trg_audit_logs_immutable
    BEFORE UPDATE OR DELETE OR TRUNCATE ON audit_logs
    FOR EACH STATEMENT EXECUTE FUNCTION prevent_immutable_table_modification()");

        // outbox_messages com índice parcial
        await ExecuteNonQueryAsync(conn, @"
CREATE TABLE IF NOT EXISTS outbox_messages (
    id           UUID NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    tenant_id    UUID NOT NULL,
    event_type   TEXT NOT NULL,
    payload_json JSONB NOT NULL,
    occurred_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    published_at TIMESTAMPTZ
)");

        await ExecuteNonQueryAsync(conn, @"
CREATE INDEX IF NOT EXISTS idx_outbox_unpublished
    ON outbox_messages (occurred_at)
    WHERE published_at IS NULL");

        // idempotency_keys
        await ExecuteNonQueryAsync(conn, @"
CREATE TABLE IF NOT EXISTS idempotency_keys (
    tenant_id       UUID NOT NULL,
    idempotency_key TEXT NOT NULL,
    request_hash    TEXT NOT NULL,
    response_ref    UUID,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (tenant_id, idempotency_key)
)");

        // bu_id em accounts (caso EnsureCreated não tenha aplicado a migration de BU)
        await ExecuteNonQueryAsync(conn, @"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'accounts' AND column_name = 'bu_id'
    ) THEN
        ALTER TABLE accounts ADD COLUMN bu_id UUID NOT NULL DEFAULT gen_random_uuid();
        ALTER TABLE accounts ALTER COLUMN bu_id DROP DEFAULT;
    END IF;
END;
$$");

        await ExecuteNonQueryAsync(conn, @"
CREATE INDEX IF NOT EXISTS idx_accounts_tenant_bu
    ON accounts (tenant_id, bu_id)");
    }

    private static async Task ExecuteNonQueryAsync(NpgsqlConnection connection, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }
}

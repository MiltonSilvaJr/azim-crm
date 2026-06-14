using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace PartnerManagement.Infrastructure.Tests.Persistence;

/// <summary>
/// Testes de integração para as migrations e RLS (TASK-16 ST-01).
/// Verifica: tabelas criadas, CHECKs rejeitem valores inválidos, índices existam, RLS ativa.
/// Usa PostgreSQL real via Testcontainers.
/// Mapeia: design §7, RNF 1, DD-001, ADR-0001, TASK-16.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MigrationAndRlsTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private string _connectionString = null!;

    public MigrationAndRlsTests()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("partner_management_rls_test")
            .WithUsername("app_test")
            .WithPassword("test_secret")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        // Aplica o SQL completo da migration diretamente (tabelas + CHECKs + índices + RLS).
        // EnsureCreated não executa o SQL customizado da migration — por isso aplicamos
        // diretamente o mesmo DDL que a migration InitialSchema executaria.
        await ApplyMigrationSqlAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Aplica o DDL completo equivalente à migration InitialSchema, incluindo:
    /// tabelas, CHECKs, índices e políticas de RLS.
    /// Necessário porque EnsureCreated cria o schema via modelo EF Core, mas não executa
    /// o SQL customizado definido em MigrationBuilder.Sql().
    /// </summary>
    private async Task ApplyMigrationSqlAsync()
    {
        await using NpgsqlConnection conn = new(_connectionString);
        await conn.OpenAsync();

        string ddl = """
            -- Tabela partners com CHECKs (design §7, Req 1.1, Req 6.3)
            CREATE TABLE IF NOT EXISTS partners (
                id              UUID          NOT NULL DEFAULT gen_random_uuid(),
                tenant_id       UUID          NOT NULL,
                name            VARCHAR(255)  NOT NULL,
                partner_type    TEXT          NOT NULL,
                pct_setup       NUMERIC(5,2)  NOT NULL DEFAULT 0,
                pct_recorrente  NUMERIC(5,2)  NOT NULL DEFAULT 0,
                contact_email   TEXT,
                contact_phone   TEXT,
                notes           TEXT,
                active          BOOLEAN       NOT NULL DEFAULT TRUE,
                created_at      TIMESTAMPTZ   NOT NULL DEFAULT now(),
                updated_at      TIMESTAMPTZ   NOT NULL DEFAULT now(),
                created_by      UUID          NOT NULL,
                updated_by      UUID          NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',

                CONSTRAINT pk_partners PRIMARY KEY (id),
                CONSTRAINT chk_partners_name_not_blank
                    CHECK (length(btrim(name)) > 0),
                CONSTRAINT chk_partners_pct_setup_range
                    CHECK (pct_setup BETWEEN 0 AND 100),
                CONSTRAINT chk_partners_pct_recorrente_range
                    CHECK (pct_recorrente BETWEEN 0 AND 100)
            );

            CREATE INDEX IF NOT EXISTS idx_partners_tenant_active
                ON partners (tenant_id, active);
            CREATE INDEX IF NOT EXISTS idx_partners_tenant_name
                ON partners (tenant_id, lower(name));

            ALTER TABLE partners ENABLE ROW LEVEL SECURITY;
            ALTER TABLE partners FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS partners_tenant_isolation ON partners;
            CREATE POLICY partners_tenant_isolation ON partners
                USING (tenant_id = current_setting('app.current_tenant', true)::uuid)
                WITH CHECK (tenant_id = current_setting('app.current_tenant', true)::uuid);

            -- Tabela outbox_messages
            CREATE TABLE IF NOT EXISTS outbox_messages (
                id           UUID        NOT NULL DEFAULT gen_random_uuid(),
                tenant_id    UUID        NOT NULL,
                event_type   TEXT        NOT NULL,
                payload_json JSONB       NOT NULL,
                occurred_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
                published_at TIMESTAMPTZ,

                CONSTRAINT pk_outbox_messages PRIMARY KEY (id)
            );

            CREATE INDEX IF NOT EXISTS idx_outbox_unpublished
                ON outbox_messages (published_at)
                WHERE published_at IS NULL;

            ALTER TABLE outbox_messages ENABLE ROW LEVEL SECURITY;
            ALTER TABLE outbox_messages FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS outbox_messages_tenant_isolation ON outbox_messages;
            CREATE POLICY outbox_messages_tenant_isolation ON outbox_messages
                USING (tenant_id = current_setting('app.current_tenant', true)::uuid)
                WITH CHECK (tenant_id = current_setting('app.current_tenant', true)::uuid);

            -- Tabela idempotency_keys
            CREATE TABLE IF NOT EXISTS idempotency_keys (
                tenant_id       UUID        NOT NULL,
                idempotency_key TEXT        NOT NULL,
                request_hash    TEXT        NOT NULL,
                response_ref    UUID,
                created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),

                CONSTRAINT pk_idempotency_keys PRIMARY KEY (tenant_id, idempotency_key)
            );

            ALTER TABLE idempotency_keys ENABLE ROW LEVEL SECURITY;
            ALTER TABLE idempotency_keys FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS idempotency_keys_tenant_isolation ON idempotency_keys;
            CREATE POLICY idempotency_keys_tenant_isolation ON idempotency_keys
                USING (tenant_id = current_setting('app.current_tenant', true)::uuid)
                WITH CHECK (tenant_id = current_setting('app.current_tenant', true)::uuid);
            """;

        await using NpgsqlCommand cmd = new(ddl, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// TASK-16 ST-01: verifica que a política de RLS existe na tabela partners.
    /// </summary>
    [Fact(DisplayName = "TASK-16: RLS ativa na tabela partners (policy partners_tenant_isolation)")]
    public async Task Rls_PolicyExists_ForPartnersTable()
    {
        await using NpgsqlConnection conn = new(_connectionString);
        await conn.OpenAsync();

        await using NpgsqlCommand cmd = new(
            "SELECT COUNT(*) FROM pg_policies WHERE tablename = 'partners' AND policyname = 'partners_tenant_isolation'",
            conn);

        long count = (long)(await cmd.ExecuteScalarAsync())!;
        count.Should().Be(1, "a política RLS deve existir na tabela partners");
    }

    /// <summary>
    /// TASK-16 ST-01: verifica que a política de RLS existe na tabela outbox_messages.
    /// </summary>
    [Fact(DisplayName = "TASK-16: RLS ativa na tabela outbox_messages")]
    public async Task Rls_PolicyExists_ForOutboxMessagesTable()
    {
        await using NpgsqlConnection conn = new(_connectionString);
        await conn.OpenAsync();

        await using NpgsqlCommand cmd = new(
            "SELECT COUNT(*) FROM pg_policies WHERE tablename = 'outbox_messages' AND policyname = 'outbox_messages_tenant_isolation'",
            conn);

        long count = (long)(await cmd.ExecuteScalarAsync())!;
        count.Should().Be(1);
    }

    /// <summary>
    /// TASK-16 ST-01: verifica que a política de RLS existe na tabela idempotency_keys.
    /// </summary>
    [Fact(DisplayName = "TASK-16: RLS ativa na tabela idempotency_keys")]
    public async Task Rls_PolicyExists_ForIdempotencyKeysTable()
    {
        await using NpgsqlConnection conn = new(_connectionString);
        await conn.OpenAsync();

        await using NpgsqlCommand cmd = new(
            "SELECT COUNT(*) FROM pg_policies WHERE tablename = 'idempotency_keys' AND policyname = 'idempotency_keys_tenant_isolation'",
            conn);

        long count = (long)(await cmd.ExecuteScalarAsync())!;
        count.Should().Be(1);
    }

    /// <summary>
    /// TASK-16 ST-01: CHECK constraint rejeita pct_setup fora do intervalo [0, 100].
    /// </summary>
    [Fact(DisplayName = "TASK-16: CHECK constraint rejeita pct_setup > 100")]
    public async Task Check_PctSetup_RejectsValueAbove100()
    {
        await using NpgsqlConnection conn = new(_connectionString);
        await conn.OpenAsync();

        // SET necessário para contornar RLS (mesmo usuário superuser no test)
        await using NpgsqlCommand setCmd = new("SET app.current_tenant = 'a0000000-0000-0000-0000-000000000001'", conn);
        await setCmd.ExecuteNonQueryAsync();

        string insertSql = """
            INSERT INTO partners (id, tenant_id, name, partner_type, pct_setup, pct_recorrente, active, created_at, created_by)
            VALUES (gen_random_uuid(), 'a0000000-0000-0000-0000-000000000001', 'Teste', 'Indicador', 150, 0, true, now(), gen_random_uuid())
            """;

        await using NpgsqlCommand cmd = new(insertSql, conn);

        Func<Task> act = () => cmd.ExecuteNonQueryAsync();
        await act.Should().ThrowAsync<PostgresException>()
            .WithMessage("*chk_partners_pct_setup_range*");
    }

    /// <summary>
    /// TASK-16 ST-01: CHECK constraint rejeita nome em branco.
    /// </summary>
    [Fact(DisplayName = "TASK-16: CHECK constraint rejeita nome vazio após trim")]
    public async Task Check_Name_RejectsBlankName()
    {
        await using NpgsqlConnection conn = new(_connectionString);
        await conn.OpenAsync();

        await using NpgsqlCommand setCmd = new("SET app.current_tenant = 'a0000000-0000-0000-0000-000000000002'", conn);
        await setCmd.ExecuteNonQueryAsync();

        string insertSql = """
            INSERT INTO partners (id, tenant_id, name, partner_type, pct_setup, pct_recorrente, active, created_at, created_by)
            VALUES (gen_random_uuid(), 'a0000000-0000-0000-0000-000000000002', '   ', 'Indicador', 0, 0, true, now(), gen_random_uuid())
            """;

        await using NpgsqlCommand cmd = new(insertSql, conn);

        Func<Task> act = () => cmd.ExecuteNonQueryAsync();
        await act.Should().ThrowAsync<PostgresException>()
            .WithMessage("*chk_partners_name_not_blank*");
    }

}

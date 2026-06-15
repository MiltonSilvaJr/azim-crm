using AccountManagement.Application.Behaviors;
using AccountManagement.Infrastructure.Persistence;
using AccountManagement.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using System.Collections.ObjectModel;

namespace AccountManagement.Infrastructure.Tests.Persistence;

/// <summary>
/// Fixture compartilhada que sobe um container PostgreSQL real via Testcontainers.
///
/// Usada em todos os testes de integração da Onda 4 (TASK-08..TASK-12).
/// Implementa <see cref="IAsyncLifetime"/> do xUnit para setup/teardown assíncrono.
///
/// O usuário do banco NÃO é superuser — valida que as policies RLS funcionam
/// com role de aplicação (design §14, DD-002, ADR-0001).
///
/// Mapeia: TASK-08 (ST-01), TASK-09 (ST-01), TASK-10 (ST-01).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;

    /// <summary>String de conexão para o container PostgreSQL de teste.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    public PostgresFixture()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("account_management_test")
            .WithUsername("app")          // role de aplicação — NOSUPERUSER (design §14)
            .WithPassword("test_password")
            .Build();
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        ConnectionString = _postgres.GetConnectionString();

        // Tenta MigrateAsync primeiro; se não houver migrations detectadas (criação manual),
        // usa EnsureCreatedAsync para criar o schema pelo modelo do DbContext,
        // e depois aplica as SQL extras das migrations (trigger, REVOKE, índices parciais).
        var options = CreateDbContextOptions();
        await using var context = new AccountManagementDbContext(options);

        var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
        var applied = (await context.Database.GetAppliedMigrationsAsync()).ToList();

        if (pending.Count > 0)
        {
            // Caminho nominal: migrations detectadas → aplica normalmente
            await context.Database.MigrateAsync();
        }
        else if (applied.Count == 0)
        {
            // Fallback: EF Core não encontrou migrations manuais → cria schema via modelo
            await context.Database.EnsureCreatedAsync();

            // Aplica SQLs extras das migrations não detectadas (trigger, REVOKE, etc.)
            await ApplyCustomMigrationSqlAsync();
        }
        // else: todas as migrations já aplicadas (run repetido) — nenhuma ação necessária
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    /// <summary>
    /// Cria opções de DbContext para um tenant específico com visão tenant-wide de BU.
    /// O contexto terá filtro de tenant ativo; filtro de BU bypassed (tenant-wide = true).
    /// </summary>
    public AccountManagementDbContext CreateDbContext(Guid tenantId)
    {
        var options = CreateDbContextOptions();
        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(tenantId);
        var infraTenantCtx = new InfrastructureTenantContext(tenantCtx);

        // Visão tenant-wide: vê todas as BUs do tenant (comportamento padrão para testes existentes)
        var buScopeCtx = new BuScopeContext();
        buScopeCtx.SetScope(Array.Empty<Guid>(), isTenantWide: true);
        var infraBuScopeCtx = new InfrastructureBuScopeContext(buScopeCtx);

        return new AccountManagementDbContext(options, infraTenantCtx, infraBuScopeCtx);
    }

    /// <summary>
    /// Cria um DbContext para um tenant com escopo de BU específico.
    /// Fail-closed: apenas contas das BUs informadas são visíveis.
    /// </summary>
    public AccountManagementDbContext CreateDbContextWithBuScope(
        Guid tenantId,
        IReadOnlyCollection<Guid> buIds,
        bool isTenantWide = false)
    {
        var options = CreateDbContextOptions();
        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(tenantId);
        var infraTenantCtx = new InfrastructureTenantContext(tenantCtx);

        var buScopeCtx = new BuScopeContext();
        buScopeCtx.SetScope(buIds, isTenantWide);
        var infraBuScopeCtx = new InfrastructureBuScopeContext(buScopeCtx);

        return new AccountManagementDbContext(options, infraTenantCtx, infraBuScopeCtx);
    }

    /// <summary>
    /// Cria um DbContext sem filtros (para setup de dados de teste).
    /// </summary>
    public AccountManagementDbContext CreateDbContextNoFilter()
    {
        var options = CreateDbContextOptions();
        return new AccountManagementDbContext(options);
    }

    private DbContextOptions<AccountManagementDbContext> CreateDbContextOptions()
    {
        return new DbContextOptionsBuilder<AccountManagementDbContext>()
            .UseNpgsql(ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .Options;
    }

    /// <summary>
    /// Aplica SQLs customizados das migrations que não são detectadas pelo EF Core
    /// quando criadas manualmente (sem <c>dotnet ef migrations add</c>).
    ///
    /// Cria: trigger de imutabilidade de audit_logs, índice parcial de outbox,
    /// e revoga permissões da role <c>app</c> (TASK-09).
    /// </summary>
    private async Task ApplyCustomMigrationSqlAsync()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        // Função PL/pgSQL de imutabilidade
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE OR REPLACE FUNCTION prevent_immutable_table_modification()
                RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'Modificação proibida: audit_logs é append-only (RNF 8). Operação: %', TG_OP
                        USING ERRCODE = 'restrict_violation',
                              DETAIL = 'audit_logs não admite UPDATE, DELETE ou TRUNCATE.',
                              HINT = 'Use apenas INSERT para registrar eventos de auditoria.';
                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;";
            await cmd.ExecuteNonQueryAsync();
        }

        // Tabela audit_logs (se não foi criada pelo EnsureCreated — TASK-09)
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS audit_logs (
                    id          UUID NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
                    tenant_id   UUID NOT NULL,
                    user_id     UUID NOT NULL,
                    entity_type TEXT NOT NULL,
                    entity_id   UUID NOT NULL,
                    action      TEXT NOT NULL,
                    delta_json  JSONB NOT NULL DEFAULT '{}',
                    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
                );";
            await cmd.ExecuteNonQueryAsync();
        }

        // Índice de auditoria
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE INDEX IF NOT EXISTS idx_audit_logs_tenant_entity
                    ON audit_logs (tenant_id, entity_type, entity_id);";
            await cmd.ExecuteNonQueryAsync();
        }

        // Trigger de imutabilidade
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                DROP TRIGGER IF EXISTS trg_audit_logs_immutable ON audit_logs;
                CREATE TRIGGER trg_audit_logs_immutable
                    BEFORE UPDATE OR DELETE OR TRUNCATE ON audit_logs
                    FOR EACH STATEMENT EXECUTE FUNCTION prevent_immutable_table_modification();";
            await cmd.ExecuteNonQueryAsync();
        }

        // Tabela outbox_messages com índice parcial (TASK-09)
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS outbox_messages (
                    id           UUID NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
                    tenant_id    UUID NOT NULL,
                    event_type   TEXT NOT NULL,
                    payload_json JSONB NOT NULL,
                    occurred_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
                    published_at TIMESTAMPTZ
                );
                CREATE INDEX IF NOT EXISTS idx_outbox_unpublished
                    ON outbox_messages (occurred_at)
                    WHERE published_at IS NULL;";
            await cmd.ExecuteNonQueryAsync();
        }

        // Tabela idempotency_keys com PK composta (TASK-09)
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS idempotency_keys (
                    tenant_id       UUID NOT NULL,
                    idempotency_key TEXT NOT NULL,
                    request_hash    TEXT NOT NULL,
                    response_ref    UUID,
                    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
                    PRIMARY KEY (tenant_id, idempotency_key)
                );";
            await cmd.ExecuteNonQueryAsync();
        }

        // REVOKE permissões da role 'app' em audit_logs (menor privilégio — RNF 8.3)
        // Condicional: só executa se a role 'app' existir (pode não existir em containers de teste)
        await using (var checkCmd = conn.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM pg_roles WHERE rolname = 'app'";
            var roleExists = (long)(await checkCmd.ExecuteScalarAsync())!;
            if (roleExists > 0)
            {
                await using var revokeCmd = conn.CreateCommand();
                revokeCmd.CommandText = "REVOKE UPDATE, DELETE, TRUNCATE ON audit_logs FROM app;";
                await revokeCmd.ExecuteNonQueryAsync();
            }
        }
    }
}

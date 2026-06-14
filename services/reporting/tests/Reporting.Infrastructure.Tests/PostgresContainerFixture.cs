using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Reporting.Infrastructure.Migrations;
using Reporting.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Reporting.Infrastructure.Tests;

/// <summary>
/// Fixture compartilhada que inicia um container PostgreSQL 16 real via Testcontainers,
/// cria o schema base e aplica todas as migrations do módulo reporting.
///
/// Dois níveis de conexão:
/// <list type="bullet">
///   <item><description><c>AdminConnectionString</c> — superuser (<c>reporting_user</c>), usado para DDL e seed.</description></item>
///   <item><description><c>AppConnectionString</c> — NOSUPERUSER (<c>app_user</c>), usado nos testes de RLS (simula aplicação).</description></item>
/// </list>
///
/// O usuário <c>app_user</c> é NOSUPERUSER, portanto submetido à RLS nas tabelas autoritativas.
/// Isso replica o ambiente de produção onde a aplicação não tem privilégio de superuser.
///
/// Mapeia: TASK-18, design §13.3, ADR-0001, DD-005, PBT-03.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("reporting_test")
        .WithUsername("reporting_user")
        .WithPassword("reporting_pass")
        .Build();

    /// <summary>
    /// Connection string administrativa (superuser) — usada para DDL, seed e limpeza.
    /// NÃO use para testar isolamento RLS: superusers bypassam RLS por padrão.
    /// </summary>
    public string AdminConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Connection string da aplicação (NOSUPERUSER) — usada nos testes de RLS.
    /// O usuário <c>app_user</c> é submetido à RLS e replica o ambiente de produção.
    /// </summary>
    public string AppConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Interceptor de tenant configurado para uso nos testes de integração.
    /// Aplica <c>SET app.current_tenant</c> antes de cada query (ADR-0001, DD-005).
    /// </summary>
    public TenantConnectionInterceptor Interceptor { get; } =
        new TenantConnectionInterceptor(NullLogger<TenantConnectionInterceptor>.Instance);

    /// <summary>
    /// Cria o schema base, aplica migrations, cria usuário NOSUPERUSER e verifica RLS.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Connection string administrativa com pooling desabilitado
        AdminConnectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Pooling = false
        }.ConnectionString;

        // Aplicar schema base das tabelas autoritativas (somente para testes)
        await using var adminConn = new NpgsqlConnection(AdminConnectionString);
        await adminConn.OpenAsync();

        // Schema base inline (subset mínimo para testes — não é schema de produção)
        await adminConn.ExecuteAsync(SqlScripts.BaseSchema);

        // Aplicar migrations do módulo (views e índices)
        foreach (var (_, sql) in MigrationRunner.GetInlineScripts())
        {
            await adminConn.ExecuteAsync(sql);
        }

        // Criar usuário NOSUPERUSER para simular aplicação em produção
        // Este usuário é submetido à RLS normalmente (não é superuser)
        await adminConn.ExecuteAsync("""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_user') THEN
                    CREATE USER app_user WITH PASSWORD 'app_pass' NOSUPERUSER NOCREATEDB NOCREATEROLE;
                END IF;
            END $$;
            """);

        // Conceder permissões ao app_user nas tabelas e views
        await adminConn.ExecuteAsync("""
            GRANT CONNECT ON DATABASE reporting_test TO app_user;
            GRANT USAGE ON SCHEMA public TO app_user;
            GRANT SELECT ON ALL TABLES IN SCHEMA public TO app_user;
            GRANT INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO app_user;
            ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO app_user;
            """);

        // Connection string da aplicação (NOSUPERUSER) com pooling desabilitado
        var adminBuilder = new NpgsqlConnectionStringBuilder(AdminConnectionString);
        AppConnectionString = new NpgsqlConnectionStringBuilder
        {
            Host = adminBuilder.Host,
            Port = adminBuilder.Port,
            Database = adminBuilder.Database,
            Username = "app_user",
            Password = "app_pass",
            Pooling = false
        }.ConnectionString;
    }

    /// <summary>
    /// Para e descarta o container Docker.
    /// </summary>
    public async Task DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Abre uma nova conexão de APLICAÇÃO com o tenant configurado via <c>app.current_tenant</c>.
    /// Usa o usuário NOSUPERUSER (<c>app_user</c>) para que a RLS seja efetiva.
    /// </summary>
    public async Task<NpgsqlConnection> OpenConnectionWithTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(AppConnectionString);
        await connection.OpenAsync(cancellationToken);
        await Interceptor.ApplyAsync(connection, tenantId, cancellationToken);
        return connection;
    }

    /// <summary>
    /// Abre uma nova conexão de APLICAÇÃO SEM tenant (simula falha-fechada — RLS deve retornar zero linhas).
    /// Usa o usuário NOSUPERUSER (<c>app_user</c>) para que a RLS seja efetiva.
    /// </summary>
    public async Task<NpgsqlConnection> OpenConnectionWithoutTenantAsync(
        CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(AppConnectionString);
        await connection.OpenAsync(cancellationToken);
        // NÃO aplica app.current_tenant — simula conexão sem tenant (falha-fechada, ADR-0001)
        return connection;
    }

    /// <summary>
    /// Abre uma conexão administrativa (superuser) para operações de seed e limpeza.
    /// NÃO use para testes de isolamento RLS.
    /// </summary>
    public async Task<NpgsqlConnection> OpenAdminConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    /// <summary>
    /// Insere uma stage de teste e retorna o ID gerado.
    /// </summary>
    public async Task<Guid> InsertStageAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid buId,
        string name = "Proposta",
        string category = "open")
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            """
            INSERT INTO stages (id, tenant_id, bu_id, name, category, probability, position)
            VALUES (@id, @tenantId, @buId, @name, @category, 50, 1)
            """,
            new { id, tenantId, buId, name, category });
        return id;
    }

    /// <summary>
    /// Insere uma oportunidade de teste e retorna o ID gerado.
    /// </summary>
    public async Task<Guid> InsertOpportunityAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid buId,
        Guid ownerId,
        Guid stageId,
        long totalCents = 100000,
        long forecastCents = 50000,
        string category = "open",
        Guid? channelId = null,
        Guid? partnerId = null,
        DateTimeOffset? closedAt = null)
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            """
            INSERT INTO opportunities
                (id, tenant_id, bu_id, owner_id, stage_id, origin_channel_id, partner_id,
                 stage_category, valor_total, forecast_ponderado, created_at, closed_at)
            VALUES
                (@id, @tenantId, @buId, @ownerId, @stageId, @channelId, @partnerId,
                 @category, @totalCents, @forecastCents, NOW(), @closedAt)
            """,
            new { id, tenantId, buId, ownerId, stageId, channelId, partnerId,
                  category, totalCents, forecastCents, closedAt });
        return id;
    }

    /// <summary>
    /// Insere um parceiro de teste e retorna o ID gerado.
    /// </summary>
    public async Task<Guid> InsertPartnerAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string name = "Parceiro Teste")
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO partners (id, tenant_id, name) VALUES (@id, @tenantId, @name)",
            new { id, tenantId, name });
        return id;
    }

    /// <summary>
    /// Insere uma comissão de teste e retorna o ID gerado.
    /// </summary>
    public async Task<Guid> InsertCommissionAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid opportunityId,
        Guid partnerId,
        long commissionCents = 10000,
        bool isSnapshot = false)
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            """
            INSERT INTO opportunity_partner_commissions
                (id, tenant_id, opportunity_id, partner_id, comissao_calculada, is_snapshot)
            VALUES (@id, @tenantId, @opportunityId, @partnerId, @commissionCents, @isSnapshot)
            """,
            new { id, tenantId, opportunityId, partnerId, commissionCents, isSnapshot });
        return id;
    }

    /// <summary>
    /// Insere um usuário de teste e retorna o ID gerado.
    /// </summary>
    public async Task<Guid> InsertUserAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string displayName = "Teste Usuario")
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO users (id, tenant_id, display_name) VALUES (@id, @tenantId, @displayName)",
            new { id, tenantId, displayName });
        return id;
    }

    /// <summary>
    /// Insere um canal de origem e retorna o ID gerado.
    /// </summary>
    public async Task<Guid> InsertChannelAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string name = "Canal Teste")
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO origin_channels (id, tenant_id, name) VALUES (@id, @tenantId, @name)",
            new { id, tenantId, name });
        return id;
    }

    /// <summary>
    /// Limpa todos os dados de teste (sem dropar as tabelas).
    /// Usa a conexão administrativa (superuser) com <c>SET row_security = off</c>.
    /// </summary>
    public async Task CleanDataAsync()
    {
        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync("""
            SET row_security = off;
            DELETE FROM opportunity_partner_commissions;
            DELETE FROM opportunities;
            DELETE FROM stages;
            DELETE FROM partners;
            DELETE FROM goals;
            DELETE FROM origin_channels;
            DELETE FROM users;
            SET row_security = on;
            """);
    }
}

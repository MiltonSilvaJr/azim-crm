using Testcontainers.PostgreSql;
using Xunit;

namespace AuditLog.Infrastructure.Tests.Fixtures;

/// <summary>
/// Fixture que sobe um container PostgreSQL real via Testcontainers.
/// Container singleton por coleção de testes — cada teste trunca os dados via superuser.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;

    public PostgresContainerFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("audit_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithCleanUp(true)
            .Build();
    }

    /// <summary>Connection string do superuser para setup (criar role, migration).</summary>
    public string SuperuserConnectionString => _container.GetConnectionString();

    /// <summary>
    /// Connection string para o role <c>app</c> (sem UPDATE/DELETE/TRUNCATE).
    /// Criado em <see cref="InitializeAsync"/> após o container estar pronto.
    /// </summary>
    public string AppConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Cria o role 'app' com INSERT e SELECT apenas
        await using var conn = new Npgsql.NpgsqlConnection(SuperuserConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'app') THEN
                    CREATE ROLE app LOGIN PASSWORD 'app';
                END IF;
            END
            $$;
            GRANT CONNECT ON DATABASE audit_test TO app;
            GRANT USAGE ON SCHEMA public TO app;
            """;
        await cmd.ExecuteNonQueryAsync();

        // Constrói a connection string do role 'app'
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(SuperuserConnectionString)
        {
            Username = "app",
            Password = "app"
        };
        AppConnectionString = builder.ToString();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Remove todos os registros de audit_logs via superuser entre testes.
    /// Desabilita temporariamente o trigger de imutabilidade (apenas superuser pode fazer isso),
    /// executa DELETE e reabilita o trigger.
    /// </summary>
    public async Task TruncateAuditLogsAsync()
    {
        await using var conn = new Npgsql.NpgsqlConnection(SuperuserConnectionString);
        await conn.OpenAsync();

        // Desabilita o trigger para permitir limpeza de testes
        await using var disableCmd = conn.CreateCommand();
        disableCmd.CommandText = "ALTER TABLE audit_logs DISABLE TRIGGER trg_audit_logs_immutable;";
        await disableCmd.ExecuteNonQueryAsync();

        // Remove todos os registros
        await using var deleteCmd = conn.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM audit_logs;";
        await deleteCmd.ExecuteNonQueryAsync();

        // Reabilita o trigger após limpeza
        await using var enableCmd = conn.CreateCommand();
        enableCmd.CommandText = "ALTER TABLE audit_logs ENABLE TRIGGER trg_audit_logs_immutable;";
        await enableCmd.ExecuteNonQueryAsync();
    }
}

/// <summary>Coleção compartilhada para os testes de infraestrutura.</summary>
[CollectionDefinition(PostgresTestCollection.Name)]
public sealed class PostgresTestCollection : ICollectionFixture<PostgresContainerFixture>
{
    public const string Name = "PostgresInfrastructureTests";
}

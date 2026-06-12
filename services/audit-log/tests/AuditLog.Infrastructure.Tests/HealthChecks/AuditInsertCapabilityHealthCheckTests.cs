using AuditLog.Infrastructure.HealthChecks;
using AuditLog.Infrastructure.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace AuditLog.Infrastructure.Tests.HealthChecks;

/// <summary>
/// Testes de integração do <see cref="AuditInsertCapabilityHealthCheck"/> com PostgreSQL real
/// via Testcontainers (design §11.5, RNF-006).
/// <para>
/// Verifica os três estados: Healthy (banco ok + INSERT permitido),
/// Unhealthy (banco inacessível) e Degraded (banco ok mas INSERT sem permissão).
/// </para>
/// </summary>
[Collection(PostgresTestCollection.Name)]
public sealed class AuditInsertCapabilityHealthCheckTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;

    public AuditInsertCapabilityHealthCheckTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await TestDbContextFactory.MigrateAsync(_fixture.SuperuserConnectionString);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // -----------------------------------------------------------------------
    // Estado Healthy — banco disponível + INSERT permitido (superuser)
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Deve retornar Healthy quando banco disponível e INSERT permitido")]
    public async Task Should_Return_Healthy_When_Bank_Available_And_Insert_Permitted()
    {
        // Usa DbContext com superuser — tem conectividade e permissão de INSERT
        await using var ctx = TestDbContextFactory.Create(_fixture.SuperuserConnectionString, Guid.NewGuid());

        var healthCheck = new AuditInsertCapabilityHealthCheck(ctx);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test", healthCheck, HealthStatus.Unhealthy, null)
        };

        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Healthy,
            because: "o superuser tem conectividade e permissão de INSERT em audit_logs");
    }

    // -----------------------------------------------------------------------
    // Estado Unhealthy — banco inacessível
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Deve retornar Unhealthy quando banco está inacessível")]
    public async Task Should_Return_Unhealthy_When_Database_Inaccessible()
    {
        // Connection string inválida para simular banco indisponível
        var badConnectionString = "Host=localhost;Port=19999;Database=nonexistent;Username=nobody;Password=bad;Timeout=1";
        await using var ctx = TestDbContextFactory.Create(badConnectionString, Guid.NewGuid());

        var healthCheck = new AuditInsertCapabilityHealthCheck(ctx);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test", healthCheck, HealthStatus.Unhealthy, null)
        };

        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy,
            because: "quando o banco está inacessível, o health check deve retornar Unhealthy");
    }

    // -----------------------------------------------------------------------
    // Estado Degraded — banco disponível mas INSERT sem permissão
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Deve retornar Degraded quando banco disponível mas INSERT não está permitido")]
    public async Task Should_Return_Degraded_When_Insert_Permission_Revoked()
    {
        // Usa a connection string do role 'app' — que tem REVOKE de INSERT após migração
        // Nota: O role 'app' tem SELECT mas não INSERT por design (DD-002, RNF-005)
        // Porém na migration temos: GRANT INSERT SELECT para 'app' e REVOKE UPDATE DELETE TRUNCATE
        // O 'app' PODE fazer INSERT — o REVOKE é de UPDATE/DELETE/TRUNCATE.
        // Para simular INSERT sem permissão: usamos uma connection string de role sem INSERT.
        // Criamos um role dedicado sem INSERT para o teste.

        await using var setupConn = new Npgsql.NpgsqlConnection(_fixture.SuperuserConnectionString);
        await setupConn.OpenAsync();

        // Cria role readonly sem INSERT
        await using var createRoleCmd = setupConn.CreateCommand();
        createRoleCmd.CommandText = """
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'app_readonly_test') THEN
                    CREATE ROLE app_readonly_test LOGIN PASSWORD 'readonly123';
                END IF;
            END
            $$;
            GRANT CONNECT ON DATABASE audit_test TO app_readonly_test;
            GRANT USAGE ON SCHEMA public TO app_readonly_test;
            GRANT SELECT ON audit_logs TO app_readonly_test;
            -- SEM INSERT — simula o estado Degraded
            """;
        await createRoleCmd.ExecuteNonQueryAsync();
        await setupConn.CloseAsync();

        var readonlyConnString = new Npgsql.NpgsqlConnectionStringBuilder(_fixture.SuperuserConnectionString)
        {
            Username = "app_readonly_test",
            Password = "readonly123"
        }.ToString();

        await using var ctx = TestDbContextFactory.Create(readonlyConnString, Guid.NewGuid());

        var healthCheck = new AuditInsertCapabilityHealthCheck(ctx);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test", healthCheck, HealthStatus.Unhealthy, null)
        };

        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Degraded,
            because: "quando banco está disponível mas INSERT não está permitido, o health check deve retornar Degraded");
    }
}

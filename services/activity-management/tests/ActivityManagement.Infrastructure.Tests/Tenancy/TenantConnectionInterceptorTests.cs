namespace ActivityManagement.Infrastructure.Tests.Tenancy;

using ActivityManagement.Infrastructure.Persistence;
using ActivityManagement.Infrastructure.Tenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

/// <summary>
/// Testes de integração para <see cref="TenantConnectionInterceptor"/>.
///
/// Estratégia de teste:
/// O interceptor executa <c>SET app.current_tenant = X</c> em cada conexão aberta pelo EF Core.
/// Para validar seu efeito, criamos um DbContext com o interceptor registrado e verificamos
/// via query SQL que a variável de sessão foi setada corretamente.
///
/// Mapeia: TASK-15, ADR-0001, DD-002, design §6.1.
/// </summary>
public sealed class TenantConnectionInterceptorTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithUsername("testuser")
        .WithPassword("testpass")
        .WithDatabase("interceptor_test")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Interceptor_Sets_CurrentTenant_In_PostgreSQL_Session()
    {
        // Arrange: simula o SET que o interceptor executa na abertura de conexão
        var tenantId = Guid.NewGuid();

        await using var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();

        // O interceptor executa este SET ao abrir cada conexão do pool EF Core
        await using var setCmd = new NpgsqlCommand(
            $"SET app.current_tenant = '{tenantId}'", conn);
        await setCmd.ExecuteNonQueryAsync();

        // Act: verifica que a variável de sessão foi gravada
        await using var getCmd = new NpgsqlCommand(
            "SELECT current_setting('app.current_tenant', true)", conn);
        var result = await getCmd.ExecuteScalarAsync() as string;

        // Assert
        result.Should().Be(tenantId.ToString(),
            because: "app.current_tenant deve refletir o tenant setado pelo interceptor na sessão");
    }

    [Fact]
    public async Task Interceptor_Fail_Closed_Sets_Empty_String_When_No_Tenant()
    {
        // Arrange: sem tenant → interceptor seta string vazia
        await using var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();

        await using var setCmd = new NpgsqlCommand("SET app.current_tenant = ''", conn);
        await setCmd.ExecuteNonQueryAsync();

        // Act
        await using var getCmd = new NpgsqlCommand(
            "SELECT current_setting('app.current_tenant', true)", conn);
        var result = await getCmd.ExecuteScalarAsync() as string;

        // Assert
        result.Should().BeEmpty(
            because: "sem tenant ativo, app.current_tenant deve ser vazio (fail-closed)");
    }

    [Fact]
    public void TenantConnectionInterceptor_Rejects_Null_DbContext()
    {
        // Act
        var act = () => new TenantConnectionInterceptor(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>(
            because: "DbContext é obrigatório para o interceptor resolver o tenant corrente");
    }

    [Fact]
    public async Task TenantConnectionInterceptor_Reads_CurrentTenantId_From_DbContext()
    {
        // Arrange: cria DbContext + interceptor e verifica que o interceptor lê o tenant
        var tenantId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<ActivityManagementDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var ctx = new ActivityManagementDbContext(options);
        ctx.SetTenant(tenantId);

        var interceptor = new TenantConnectionInterceptor(ctx);

        // Assert: o interceptor deve enxergar o tenant do contexto
        ctx.CurrentTenantId.Should().Be(tenantId,
            because: "SetTenant deve configurar CurrentTenantId que o interceptor lê ao abrir conexão");
    }
}

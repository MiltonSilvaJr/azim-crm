using Microsoft.EntityFrameworkCore;
using PartnerManagement.Application.Ports;
using PartnerManagement.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace PartnerManagement.Infrastructure.Tests.Fixtures;

/// <summary>
/// Fixture compartilhada que inicia um container PostgreSQL real via Testcontainers.
/// Utilizada em todos os testes de integração da Onda 4 (TASK-15..TASK-21).
/// O container é iniciado uma vez por coleção e derrubado ao final.
/// Mapeia: design §13, TASK-15, TASK-16, TASK-17, TASK-21.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;

    public PostgresContainerFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("partner_management_test")
            .WithUsername("app_test")
            .WithPassword("test_secret")
            .Build();
    }

    /// <summary>String de conexão ao container PostgreSQL.</summary>
    public string ConnectionString { get; private set; } = null!;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Cria um <see cref="PartnerManagementDbContext"/> com o tenant informado e migrations aplicadas.
    /// Cada chamada aplica as migrations para garantir schema atualizado.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant para o contexto.</param>
    public PartnerManagementDbContext CreateDbContext(Guid tenantId)
    {
        DbContextOptions<PartnerManagementDbContext> options = new DbContextOptionsBuilder<PartnerManagementDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        ITenantContext tenantContext = new StaticTenantContext(tenantId);
        return new PartnerManagementDbContext(options, tenantContext);
    }

    /// <summary>
    /// Aplica as migrations no banco do container.
    /// Deve ser chamado uma vez por test class antes dos testes.
    /// </summary>
    public async Task ApplyMigrationsAsync()
    {
        using PartnerManagementDbContext ctx = CreateDbContext(Guid.NewGuid());
        await ctx.Database.MigrateAsync();
    }
}

/// <summary>
/// Implementação estática de <see cref="ITenantContext"/> para uso nos testes de integração.
/// Permite injetar um tenant fixo sem depender de HTTP context.
/// </summary>
internal sealed class StaticTenantContext : ITenantContext
{
    public StaticTenantContext(Guid tenantId)
    {
        CurrentTenantId = tenantId;
    }

    /// <inheritdoc/>
    public Guid CurrentTenantId { get; }
}

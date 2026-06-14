using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PartnerManagement.Application.Ports;

namespace PartnerManagement.Infrastructure.Persistence;

/// <summary>
/// Factory de design-time para o EF Core tools (migrations).
/// Usada apenas por <c>dotnet ef migrations add</c> e <c>dotnet ef database update</c>.
/// Não é usada em runtime — o DbContext real é criado via DI.
/// Mapeia: TASK-15, TASK-16.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PartnerManagementDbContext>
{
    /// <inheritdoc/>
    public PartnerManagementDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<PartnerManagementDbContext> optionsBuilder =
            new DbContextOptionsBuilder<PartnerManagementDbContext>()
                .UseNpgsql(
                    "Host=localhost;Database=partner_management_dev;Username=postgres;Password=postgres",
                    npgsql => npgsql.MigrationsAssembly(typeof(PartnerManagementDbContext).Assembly.GetName().Name));

        // Tenant de design-time: Guid.Empty (não executa queries reais)
        ITenantContext tenantContext = new DesignTimeTenantContext();

        return new PartnerManagementDbContext(optionsBuilder.Options, tenantContext);
    }

    /// <summary>
    /// Contexto de tenant para design-time: sem tenant real.
    /// Usado apenas durante geração de migrations — nunca em runtime.
    /// </summary>
    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid CurrentTenantId => Guid.Empty;
    }
}

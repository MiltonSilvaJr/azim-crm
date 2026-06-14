using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Organization.Infrastructure.Persistence;

/// <summary>
/// Factory para uso em tempo de design (migrations via CLI do EF Core).
/// Não utilizada em produção — a instância real é criada via DI com <see cref="TenantContextAccessor"/> injetado.
/// </summary>
public sealed class OrganizationDbContextFactory : IDesignTimeDbContextFactory<OrganizationDbContext>
{
    /// <inheritdoc/>
    public OrganizationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseNpgsql(
                args.Length > 0
                    ? args[0]
                    : "Host=localhost;Database=azim_org_dev;Username=org_app;Password=dev_password",
                npgsql => npgsql.MigrationsAssembly("Organization.Infrastructure"))
            .Options;

        // Contexto de design: sem filtro ativo (IsEnabled = false)
        var accessor = new TenantContextAccessor { IsEnabled = false };
        return new OrganizationDbContext(options, accessor);
    }
}

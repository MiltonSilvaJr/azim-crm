namespace ActivityManagement.Infrastructure.Persistence;

using ActivityManagement.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Factory para uso em tempo de design pelas ferramentas EF Core (<c>dotnet ef</c>).
/// Não é usada em tempo de execução — apenas para geração de migrations.
/// Mapeia: design §6.1, TASK-13.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ActivityManagementDbContext>
{
    public ActivityManagementDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ActivityManagementDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=activitymanagement_design;Username=postgres;Password=postgres",
                npgsql => npgsql.MigrationsAssembly(typeof(DesignTimeDbContextFactory).Assembly.FullName))
            .Options;

        var context = new ActivityManagementDbContext(options);
        return context;
    }
}

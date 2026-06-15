namespace ActivityManagement.Infrastructure.Persistence;

using ActivityManagement.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Factory para uso em tempo de design pelas ferramentas EF Core (<c>dotnet ef</c>).
/// Não é usada em tempo de execução — apenas para geração de migrations.
/// ADR-0006: aponta para o banco compartilhado (azim_shared) que é também usado pelo digest.
/// As migrations do activity-management não criam nem alteram digest_action_tokens
/// (tabela mapeada com ExcludeFromMigrations).
/// Mapeia: design §6.1, ADR-0006, TASK-13.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ActivityManagementDbContext>
{
    public ActivityManagementDbContext CreateDbContext(string[] args)
    {
        // ADR-0006: banco compartilhado com o digest (azim_shared).
        var options = new DbContextOptionsBuilder<ActivityManagementDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=azim_shared;Username=postgres;Password=postgres",
                npgsql => npgsql.MigrationsAssembly(typeof(DesignTimeDbContextFactory).Assembly.FullName))
            .Options;

        var context = new ActivityManagementDbContext(options);
        return context;
    }
}

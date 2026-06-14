using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GoalForecast.Infrastructure.Persistence;

/// <summary>
/// Factory de design-time para geração de migrations via <c>dotnet ef</c>.
/// Usa Guid.Empty como tenant placeholder (migrations não executam queries filtrando tenant).
/// Mapeia: TASK-15, TASK-16, design §6.1.
/// </summary>
public sealed class GoalForecastDbContextFactory : IDesignTimeDbContextFactory<GoalForecastDbContext>
{
    public GoalForecastDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GoalForecastDbContext>();

        // Connection string para design-time (migrations locais).
        // Em produção, a string vem de configuração via DI.
        var connectionString = Environment.GetEnvironmentVariable("GOALFORECAST_DB")
            ?? "Host=localhost;Database=goal_forecast_dev;Username=azim;Password=azim";

        optionsBuilder.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsAssembly("GoalForecast.Infrastructure"));

        return new GoalForecastDbContext(optionsBuilder.Options, Guid.Empty);
    }
}

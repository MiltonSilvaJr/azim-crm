using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AccountManagement.Infrastructure.Persistence;

/// <summary>
/// Factory de design-time para <see cref="AccountManagementDbContext"/>.
///
/// Usada pelo <c>dotnet ef migrations add</c> sem o contexto de runtime (DI não disponível).
/// O <c>InfrastructureTenantContext</c> não é fornecido — migrações não usam filtro de tenant.
///
/// Mapeia: TASK-08 (ST-03), TASK-09 (ST-03).
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AccountManagementDbContext>
{
    /// <inheritdoc />
    public AccountManagementDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ACCOUNT_MANAGEMENT_DB_CONNECTION")
            ?? "Host=localhost;Database=account_management;Username=app;Password=changeme";

        var options = new DbContextOptionsBuilder<AccountManagementDbContext>()
            .UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .Options;

        return new AccountManagementDbContext(options);
    }
}

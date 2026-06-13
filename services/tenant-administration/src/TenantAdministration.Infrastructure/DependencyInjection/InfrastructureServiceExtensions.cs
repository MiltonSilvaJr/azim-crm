using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TenantAdministration.Application.Ports;
using TenantAdministration.Infrastructure.Clock;
using TenantAdministration.Infrastructure.Identity;
using TenantAdministration.Infrastructure.Outbox;
using TenantAdministration.Infrastructure.Persistence;
using TenantAdministration.Infrastructure.Persistence.Repositories;
using TenantAdministration.Infrastructure.Saga;
using TenantAdministration.Infrastructure.Storage;

namespace TenantAdministration.Infrastructure.DependencyInjection;

/// <summary>
/// Extensões de registro da camada de Infrastructure na DI do módulo.
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Registra todos os serviços da Infrastructure: EF Core, repositórios,
    /// saga, adapters de storage e publicador de Outbox.
    /// </summary>
    /// <param name="services">Container de serviços.</param>
    /// <param name="connectionString">Connection string do PostgreSQL.</param>
    /// <returns>O container de serviços para encadeamento.</returns>
    public static IServiceCollection AddTenantAdministrationInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        // ── DbContext ─────────────────────────────────────────────────────────
        services.AddDbContext<TenantAdministrationDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(
                    typeof(TenantAdministrationDbContext).Assembly.FullName);
            });
        });

        // ── Repositórios e Unit of Work ───────────────────────────────────────
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IIdempotencyStore, EfIdempotencyStore>();

        // ── Outbox ────────────────────────────────────────────────────────────
        services.AddScoped<IEventOutbox, OutboxRepository>();
        services.AddSingleton<IPubSubPublisher, InMemoryPubSubPublisher>();
        services.AddHostedService<OutboxPublisher>();

        // ── Saga ──────────────────────────────────────────────────────────────
        services.AddScoped<ITenantProvisioningSaga, TenantProvisioningSaga>();

        // ── Identity Platform ─────────────────────────────────────────────────
        services.AddHttpClient<GcpIdentityPlatformAdapter>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddScoped<IIdentityTenantProvisioner, GcpIdentityPlatformAdapter>();

        // ── Storage ───────────────────────────────────────────────────────────
        services.AddScoped<IBrandingAssetStorage, FakeBrandingAssetStorage>();
        services.AddScoped<ICdnInvalidator, FakeCdnInvalidator>();
        services.AddScoped<SlugTenantIdCache>();

        // ── Clock ─────────────────────────────────────────────────────────────
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}

using DataMigration.Application.Ports;
using DataMigration.Infrastructure.Adapters;
using DataMigration.Infrastructure.Outbox;
using DataMigration.Infrastructure.Parsing;
using DataMigration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataMigration.Infrastructure;

/// <summary>
/// Extensões de registro de DI da camada Infrastructure.
/// Registra todos os adaptadores, DbContext, repositório e
/// dependências de infra do módulo data-migration.
///
/// Chamado por <c>DataMigration.Api/Program.cs</c>.
///
/// Rastreia: design §3, §6.1, §6.4, DD-001, TASK-21.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adiciona todos os serviços de Infrastructure do módulo data-migration.
    /// </summary>
    public static IServiceCollection AddDataMigrationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // =========================================================================
        // ICurrentTenantContext é registrado pela API (resolve do JWT/headers).
        // Infrastructure consome a interface — não registra a implementação.
        // =========================================================================

        // =========================================================================
        // DbContext com interceptores RLS (ADR-0001, DD-008)
        // =========================================================================
        var connectionString = configuration.GetConnectionString("MigrationDb")
            ?? "Host=localhost;Database=azim_dev;Username=azim;Password=azim";

        services.AddScoped(sp =>
        {
            var tenantContext = sp.GetRequiredService<ICurrentTenantContext>();
            var tenantId = tenantContext.TenantId ?? Guid.Empty;

            // Interceptores recebem Guid do tenant (conforme assinatura)
            var interceptor = new TenantConnectionInterceptor(tenantId);
            var saveInterceptor = new TenantSaveChangesInterceptor(tenantId);

            var optionsBuilder = new DbContextOptionsBuilder<MigrationDbContext>();
            optionsBuilder
                .UseNpgsql(connectionString)
                .AddInterceptors(interceptor, saveInterceptor);

            return new MigrationDbContext(optionsBuilder.Options, tenantId);
        });

        // =========================================================================
        // Unit of Work compartilhado (DD-001)
        // =========================================================================
        services.AddScoped<IUnitOfWork, SharedUnitOfWork>();

        // =========================================================================
        // Repositório
        // =========================================================================
        services.AddScoped<IMigrationJobRepository, MigrationJobRepository>();

        // =========================================================================
        // Parser de planilha (ClosedXML, DD-002)
        // =========================================================================
        services.AddSingleton<ISpreadsheetParser, SpreadsheetParser>();

        // =========================================================================
        // Adaptadores de portas in-process (DD-001, design §6.4)
        // =========================================================================
        services.AddScoped<IAccountImportPort, AccountImportAdapter>();
        services.AddScoped<IPartnerImportPort, PartnerImportAdapter>();
        services.AddScoped<IOpportunityImportPort, OpportunityImportAdapter>();
        services.AddScoped<IOpportunityNumberPort, OpportunityNumberAdapter>();
        services.AddScoped<IActivityImportPort, ActivityImportAdapter>();
        services.AddScoped<IOrganizationReadPort, OrganizationReadAdapter>();

        // =========================================================================
        // Outbox publisher (design §6.3)
        // =========================================================================
        services.AddScoped<OutboxPublisher>();

        // =========================================================================
        // Clock
        // =========================================================================
        services.AddSingleton<IClock, SystemClock>();

        // =========================================================================
        // Feature flags (implementação padrão habilitada)
        // =========================================================================
        services.AddSingleton<IFeatureFlags, DefaultFeatureFlags>();

        return services;
    }
}

/// <summary>
/// Implementação de clock que retorna a hora real do sistema.
/// </summary>
internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>
/// Implementação padrão de feature flags para produção.
/// Habilita <c>migration.import_enabled</c> por padrão.
/// Em produção substituir por leitura de configuração ou LaunchDarkly.
/// </summary>
internal sealed class DefaultFeatureFlags : IFeatureFlags
{
    public bool IsEnabled(string flagName) => flagName switch
    {
        "migration.import_enabled" => true,
        _ => false,
    };
}

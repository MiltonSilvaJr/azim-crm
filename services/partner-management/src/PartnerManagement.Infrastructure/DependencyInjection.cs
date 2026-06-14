using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PartnerManagement.Application.Behaviors;
using PartnerManagement.Application.Ports;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.Repositories;
using PartnerManagement.Infrastructure.Audit;
using PartnerManagement.Infrastructure.Clock;
using PartnerManagement.Infrastructure.Idempotency;
using PartnerManagement.Infrastructure.Outbox;
using PartnerManagement.Infrastructure.Persistence;
using PartnerManagement.Infrastructure.ReadPorts;
using PartnerManagement.Infrastructure.Roles;
using PartnerManagement.Infrastructure.Tenancy;

namespace PartnerManagement.Infrastructure;

/// <summary>
/// Extensões de DI da camada Infrastructure para o módulo partner-management.
/// Registra: DbContext (EF Core + Npgsql), repositórios, ports de Application,
/// interceptor RLS, Outbox publisher e adapters.
/// Mapeia: design §3, design §6, TASK-15..TASK-21.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adiciona todos os serviços de Infrastructure ao contêiner de DI.
    /// </summary>
    /// <param name="services">Coleção de serviços.</param>
    /// <param name="configuration">Configuração da aplicação (connection string, etc.).</param>
    public static IServiceCollection AddPartnerManagementInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // =====================================================================
        // TenantContext — scoped (um por requisição HTTP)
        // =====================================================================
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        // =====================================================================
        // RLS session interceptor — injetado no DbContext
        // =====================================================================
        services.AddScoped<RlsSessionInterceptor>();

        // =====================================================================
        // EF Core DbContext com Npgsql (EF Core 9.0.6, Npgsql 9.0.4)
        // Interceptor RLS injeta SET app.current_tenant antes de cada comando.
        // =====================================================================
        services.AddDbContext<PartnerManagementDbContext>((sp, options) =>
        {
            string connectionString = configuration.GetConnectionString("PartnerManagement")
                ?? throw new InvalidOperationException("Connection string 'PartnerManagement' não configurada.");

            RlsSessionInterceptor rlsInterceptor = sp.GetRequiredService<RlsSessionInterceptor>();

            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(PartnerManagementDbContext).Assembly.GetName().Name))
                .AddInterceptors(rlsInterceptor);
        });

        // =====================================================================
        // Unit of Work
        // =====================================================================
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // =====================================================================
        // Repositório de parceiros
        // =====================================================================
        services.AddScoped<IPartnerRepository, PartnerRepository>();

        // =====================================================================
        // Repositório de idempotência
        // =====================================================================
        services.AddScoped<IdempotencyKeyRepository>();

        // =====================================================================
        // PII Masker — singleton (sem estado mutável)
        // =====================================================================
        services.AddSingleton<PartnerPiiMasker>();

        // =====================================================================
        // AuditPublisher
        // =====================================================================
        services.AddScoped<IAuditPublisher, AuditPublisher>();

        // =====================================================================
        // Clock
        // =====================================================================
        services.AddSingleton<IClock, SystemClock>();

        // =====================================================================
        // CanonicalRoleProvider — cache por tenant (IMemoryCache)
        // =====================================================================
        services.AddMemoryCache();
        services.AddSingleton<CanonicalRoleProvider>();
        services.AddSingleton<ICanonicalRoleProvider>(sp => sp.GetRequiredService<CanonicalRoleProvider>());
        services.AddSingleton<ICanonicalRoleProviderPort>(sp => sp.GetRequiredService<CanonicalRoleProvider>());

        // =====================================================================
        // PartnerCommissionReadAdapter — HttpClient para o pipeline
        // =====================================================================
        services.AddHttpClient<IPartnerCommissionReadPort, PartnerCommissionReadAdapter>(client =>
        {
            string pipelineBaseUrl = configuration["Pipeline:BaseUrl"]
                ?? "http://localhost:5001";
            client.BaseAddress = new Uri(pipelineBaseUrl);
        });

        // =====================================================================
        // Outbox relay — background service
        // =====================================================================
        services.AddHostedService<OutboxPublisher>();

        return services;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Opportunities.Queries;
using OpportunityPipeline.Application.Opportunities.Services;
using OpportunityPipeline.Application.SavedFilters;
using OpportunityPipeline.Domain.Opportunities.Ports;
using OpportunityPipeline.Domain.Opportunities.Repositories;
using OpportunityPipeline.Infrastructure.Audit;
using OpportunityPipeline.Infrastructure.Numbering;
using OpportunityPipeline.Infrastructure.Outbox;
using OpportunityPipeline.Infrastructure.Persistence;
using OpportunityPipeline.Infrastructure.Persistence.Repositories;
using OpportunityPipeline.Infrastructure.ReadPorts;
using OpportunityPipeline.Infrastructure.Scheduling;
using OpportunityPipeline.Infrastructure.Tenancy;

namespace OpportunityPipeline.Infrastructure;

/// <summary>
/// Extensões de registro da Infrastructure no DI.
/// Chamado pelo Program.cs da Api (TASK-20).
/// Mapeia: design §6, TASK-13..TASK-18.
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Registra todos os serviços de infraestrutura no DI container.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ---- EF Core / PostgreSQL ------------------------------------------
        var connectionString = configuration.GetConnectionString("OpportunityPipeline")
            ?? "Host=localhost;Database=opportunity_pipeline;Username=app;Password=dev";

        services.AddDbContext<OpportunityDbContext>((sp, opts) =>
        {
            var tenantContext = sp.GetRequiredService<Application.Common.TenantContext>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RlsConnectionInterceptor>>();

            opts.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(3);
            });
            opts.AddInterceptors(new RlsConnectionInterceptor(tenantContext, logger));
        });

        // IUnitOfWork implementado pelo DbContext (scoped)
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<OpportunityDbContext>());

        // ---- Repositories --------------------------------------------------
        services.AddScoped<IOpportunityRepository, OpportunityRepository>();
        services.AddScoped<IOpportunityQueryRepository, OpportunityQueryRepository>();
        services.AddScoped<IStaleDetectionRunRepository, StaleDetectionRunRepository>();

        // ---- Number Generator ----------------------------------------------
        services.AddScoped<IOpportunityNumberGenerator, OpportunityNumberGenerator>();

        // ---- Domain Event Dispatcher (Outbox) -----------------------------
        services.AddScoped<IDomainEventDispatcher, OutboxDomainEventDispatcher>();

        // ---- Audit ---------------------------------------------------------
        services.AddScoped<IAuditPublisher, AuditPublisher>();

        // ---- System Clock --------------------------------------------------
        services.AddSingleton<IClock, SystemClock>();

        // ---- Read Port Adapters (com Polly) --------------------------------
        services.AddHttpClient("OrganizationPort")
            .AddPolicyHandler(DI.PollyPolicies.GetRetryPolicy())
            .AddPolicyHandler(DI.PollyPolicies.GetCircuitBreakerPolicy());

        services.AddHttpClient("AccountPort")
            .AddPolicyHandler(DI.PollyPolicies.GetRetryPolicy())
            .AddPolicyHandler(DI.PollyPolicies.GetCircuitBreakerPolicy());

        services.AddHttpClient("PartnerPort")
            .AddPolicyHandler(DI.PollyPolicies.GetRetryPolicy())
            .AddPolicyHandler(DI.PollyPolicies.GetCircuitBreakerPolicy());

        services.AddHttpClient("ActivityPort")
            .AddPolicyHandler(DI.PollyPolicies.GetRetryPolicy())
            .AddPolicyHandler(DI.PollyPolicies.GetCircuitBreakerPolicy());

        services.AddScoped<IOrganizationReadPort, OrganizationReadPortAdapter>();
        services.AddScoped<IAccountReadPort, AccountReadPortAdapter>();
        services.AddScoped<IPartnerReadPort, PartnerReadPortAdapter>();
        services.AddScoped<IActivityReadPort, ActivityReadPortAdapter>();

        // ---- Outbox Publisher (background service) -------------------------
        services.AddHostedService<OutboxPublisherBackgroundService>();

        // ---- Saved Filter Repository ---------------------------------------
        services.AddScoped<ISavedFilterRepository, SavedFilterRepository>();

        // ---- Application Services ------------------------------------------
        services.AddScoped<StagnationDetectionService>();

        // ---- Stale Scan Handler (endpoint interno) -------------------------
        services.AddScoped<StaleScanEndpointHandler>();

        // ---- Idempotency Store (in-memory para dev; Redis em produção) ----
        services.AddMemoryCache();
        services.AddScoped<IIdempotencyStore, DI.InMemoryIdempotencyStore>();

        return services;
    }
}

namespace ActivityManagement.Infrastructure;

using ActivityManagement.Application.Behaviors;
using ActivityManagement.Application.Ports;
using ActivityManagement.Infrastructure.Audit;
using ActivityManagement.Infrastructure.Outbox;
using ActivityManagement.Infrastructure.Persistence;
using ActivityManagement.Infrastructure.Persistence.Repositories;
using ActivityManagement.Infrastructure.ReadPorts;
using ActivityManagement.Infrastructure.Tenancy;
using ActivityManagement.Infrastructure.Tokens;
using ActivityManagement.Domain.Activities.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Extensões de registro de dependências da camada Infrastructure.
/// Centraliza o registro de repositórios, adapters, DbContext, Outbox e auditoria.
/// Chamado pelo <c>Program.cs</c> da Api (TASK-18, design §6).
/// Os HttpClients de leitura (opportunity, account) já recebem timeout configurado;
/// resiliência via Polly (retry + circuit breaker) é adicionada quando o pacote
/// <c>Microsoft.Extensions.Http.Resilience</c> estiver disponível (Onda 6, TASK-22).
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra todos os serviços de infraestrutura no contêiner de DI.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration          configuration)
    {
        // ── DbContext + interceptor de tenant ────────────────────────────────
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=activity_management_dev;Username=app;Password=app";

        services.AddSingleton<TenantConnectionInterceptor>();

        services.AddDbContext<ActivityManagementDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString)
                   .AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>());
        });

        // ── Repositório e Unit of Work ────────────────────────────────────────
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<IUnitOfWork, ActivityManagementUnitOfWork>();

        // ── Adapters de token e auditoria ─────────────────────────────────────
        services.AddScoped<IDigestActionTokenPort, DigestActionTokenAdapter>();
        services.AddScoped<IAuditPublisher, AuditPublisher>();

        // ── Adapters de leitura (HTTP interno com timeout configurado — design §6.4) ─
        // Resiliência via Polly retry+circuit breaker será adicionada em TASK-22
        // quando Microsoft.Extensions.Http.Resilience for incorporado.
        services.AddHttpClient<IOpportunityReadPort, OpportunityReadAdapter>(client =>
        {
            client.BaseAddress = new Uri(
                configuration["Services:OpportunityPipeline:BaseUrl"]
                ?? "http://opportunity-pipeline/");
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        services.AddHttpClient<IAccountReadPort, AccountReadAdapter>(client =>
        {
            client.BaseAddress = new Uri(
                configuration["Services:AccountManagement:BaseUrl"]
                ?? "http://account-management/");
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        // ── Outbox publisher (scoped — usado pela UnitOfWork) ─────────────────
        services.AddScoped<OutboxPublisher>();

        // ── Outbox relay worker ────────────────────────────────────────────────
        services.AddHostedService(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<OutboxRelayWorker>>();
            return new OutboxRelayWorker(connectionString, logger);
        });

        // ── Health check PostgreSQL ───────────────────────────────────────────
        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgres", tags: ["ready"]);

        return services;
    }
}

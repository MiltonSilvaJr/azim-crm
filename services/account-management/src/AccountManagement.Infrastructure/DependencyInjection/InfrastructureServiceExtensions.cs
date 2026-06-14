using AccountManagement.Application.Ports;
using AccountManagement.Domain.Accounts.Repositories;
using AccountManagement.Infrastructure.Audit;
using AccountManagement.Infrastructure.Observability;
using AccountManagement.Infrastructure.Outbox;
using AccountManagement.Infrastructure.Persistence;
using AccountManagement.Infrastructure.ReadPorts;
using AccountManagement.Infrastructure.Repositories;
using AccountManagement.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccountManagement.Infrastructure.DependencyInjection;

/// <summary>
/// Extensões de DI para registrar todos os serviços da camada Infrastructure.
///
/// Chamado a partir do <c>Program.cs</c> da Api para wiring completo.
///
/// Mapeia: design §6.1..6.4, TASK-08..TASK-12.
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Registra todos os serviços de Infrastructure no container de DI.
    /// </summary>
    /// <param name="services">Container de serviços.</param>
    /// <param name="connectionString">String de conexão para o PostgreSQL.</param>
    /// <param name="opportunityBaseUrl">URL base do opportunity-pipeline.</param>
    /// <param name="activityBaseUrl">URL base do activity-management.</param>
    /// <returns>O container de serviços para encadeamento.</returns>
    public static IServiceCollection AddAccountManagementInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string opportunityBaseUrl = "http://opportunity-pipeline",
        string activityBaseUrl = "http://activity-management")
    {
        // =====================================================================
        // Persistência — DbContext + TenantContext
        // =====================================================================
        services.AddScoped<InfrastructureTenantContext>();

        services.AddDbContext<AccountManagementDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"));
        });

        // =====================================================================
        // Repositórios e UnitOfWork
        // =====================================================================
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IClock, SystemClock>();

        // =====================================================================
        // Auditoria e Outbox
        // =====================================================================
        services.AddSingleton<PiiMasker>();
        services.AddScoped<IAuditPublisher, AuditPublisher>();
        services.AddScoped<IEventPublisher, OutboxEventPublisher>();

        // =====================================================================
        // Idempotência
        // =====================================================================
        services.AddScoped<IdempotencyKeyRepository>();

        // =====================================================================
        // ReadPorts — HTTP com timeout configurado via HttpClient (resiliência — design §6.4)
        // Retry e circuit breaker são aplicados nos adaptadores via try/catch
        // (resiliência de degradação parcial implementada em OpportunityReadAdapter e ActivityReadAdapter)
        // =====================================================================
        services.AddHttpClient<IOpportunityReadPort, OpportunityReadAdapter>(c =>
        {
            c.BaseAddress = new Uri(opportunityBaseUrl);
            c.Timeout = TimeSpan.FromSeconds(3);
        });

        services.AddHttpClient<IActivityReadPort, ActivityReadAdapter>(c =>
        {
            c.BaseAddress = new Uri(activityBaseUrl);
            c.Timeout = TimeSpan.FromSeconds(3);
        });

        // =====================================================================
        // Outbox Relay Worker
        // =====================================================================
        services.AddSingleton<IOutboxBrokerPublisher, NoOpOutboxBrokerPublisher>();
        services.AddHostedService<OutboxRelayWorker>();

        // =====================================================================
        // Observabilidade — métricas, traces OpenTelemetry, health checks (TASK-16)
        // =====================================================================
        services.AddAccountManagementObservability(connectionString);

        return services;
    }
}

/// <summary>
/// Implementação stub do broker de Pub/Sub para uso sem GCP configurado.
/// Substituída pela implementação real de Cloud Pub/Sub em produção.
///
/// Mapeia: TASK-11 (ST-04), DD-007.
/// </summary>
internal sealed class NoOpOutboxBrokerPublisher : IOutboxBrokerPublisher
{
    public Task PublishAsync(
        string eventType,
        Guid tenantId,
        string payloadJson,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        // Em produção, substitua por GoogleCloudPubSubPublisher
        return Task.CompletedTask;
    }
}

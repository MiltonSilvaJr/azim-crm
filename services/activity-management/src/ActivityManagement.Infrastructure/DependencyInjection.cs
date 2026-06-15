namespace ActivityManagement.Infrastructure;

using ActivityManagement.Application.Behaviors;
using ActivityManagement.Application.Ports;
using ActivityManagement.Infrastructure.Audit;
using ActivityManagement.Infrastructure.Observability;
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
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Polly;
using Polly.Extensions.Http;

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
        // ADR-0006: activity-management e digest compartilham o MESMO banco físico.
        // A tabela digest_action_tokens é criada pelo digest; este serviço é somente consumidor.
        // Em produção, ambos os serviços apontam para o banco compartilhado "azim_shared".
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=azim_shared;Username=app;Password=app";

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

        // ── Métricas Prometheus (TASK-22, RNF 6.2) ──────────────────────────────────
        services.AddSingleton<ActivityMetrics>();
        services.AddSingleton<IActivityMetrics>(sp => sp.GetRequiredService<ActivityMetrics>());

        // ── OpenTelemetry: traces + métricas (TASK-22, design §11) ──────────────────
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(
                serviceName:    "activity-management",
                serviceVersion: "1.0.0"))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddConsoleExporter())
            .WithMetrics(metrics => metrics
                .AddMeter(ActivityMetrics.MeterName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());

        // ── Adapters de leitura com Polly (timeout + retry + circuit breaker — design §6.4) ─
        // TASK-22: dívida das ondas anteriores resolvida — Polly adicionado.
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1)));

        var circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak:                   TimeSpan.FromSeconds(30));

        services.AddHttpClient<IOpportunityReadPort, OpportunityReadAdapter>(client =>
        {
            client.BaseAddress = new Uri(
                configuration["Services:OpportunityPipeline:BaseUrl"]
                ?? "http://opportunity-pipeline/");
            client.Timeout = TimeSpan.FromSeconds(5);
        })
        .AddPolicyHandler(retryPolicy)
        .AddPolicyHandler(circuitBreakerPolicy);

        services.AddHttpClient<IAccountReadPort, AccountReadAdapter>(client =>
        {
            client.BaseAddress = new Uri(
                configuration["Services:AccountManagement:BaseUrl"]
                ?? "http://account-management/");
            client.Timeout = TimeSpan.FromSeconds(5);
        })
        .AddPolicyHandler(retryPolicy)
        .AddPolicyHandler(circuitBreakerPolicy);

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

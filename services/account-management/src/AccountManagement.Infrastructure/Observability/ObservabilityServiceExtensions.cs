using AccountManagement.Application.Observability;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AccountManagement.Infrastructure.Observability;

/// <summary>
/// Extensões de DI para configurar os três pilares de observabilidade
/// do módulo account-management (design §11, RNF 9):
///
/// 1. Métricas — <see cref="AccountMetrics"/> (contadores em memória expostos via Prometheus).
/// 2. Traces — OpenTelemetry com spans para ASP.NET Core e HTTP client.
/// 3. Health checks — Cloud SQL (PostgreSQL) e liveness/readiness.
///
/// Mapeia: TASK-16 ST-03, design §11, RNF 9.1..9.3.
/// </summary>
public static class ObservabilityServiceExtensions
{
    private const string ServiceName = "account-management";
    private const string ServiceVersion = "1.0.0";

    /// <summary>
    /// Registra métricas, traces OpenTelemetry e health checks.
    /// </summary>
    /// <param name="services">Container de DI.</param>
    /// <param name="connectionString">String de conexão PostgreSQL (usada no health check).</param>
    public static IServiceCollection AddAccountManagementObservability(
        this IServiceCollection services,
        string? connectionString = null)
    {
        // =====================================================================
        // Métricas (AccountMetrics — Singleton; thread-safe via Interlocked)
        // Exposto em /metrics via prometheus-net (configurado no Program.cs da Api)
        // =====================================================================
        services.AddSingleton<AccountMetrics>();

        // =====================================================================
        // Traces — OpenTelemetry
        // Spans: ASP.NET Core + HTTP client (ReadPorts) + handlers (manual)
        // correlation_id como atributo root do trace (RNF 9.1, design §11)
        // Sem PII em atributos de span (RNF 1)
        // =====================================================================
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(ServiceName, serviceVersion: ServiceVersion)
                .AddAttributes([
                    new KeyValuePair<string, object>("deployment.region", "southamerica-east1"),
                    new KeyValuePair<string, object>("module", "account-management"),
                ]))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    // Filtra health checks dos traces (ruído operacional)
                    options.Filter = httpContext =>
                        !httpContext.Request.Path.StartsWithSegments("/health");
                })
                .AddHttpClientInstrumentation()
                // Exportador console em desenvolvimento/testes; substituir por OTLP em produção
                .AddConsoleExporter());

        // =====================================================================
        // Health Checks — readiness e liveness (design §11)
        // =====================================================================
        var healthBuilder = services.AddHealthChecks();

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            // Readiness: verifica conectividade com Cloud SQL/PostgreSQL
            healthBuilder.AddNpgSql(
                connectionString,
                name: "postgres",
                tags: ["ready", "database"]);
        }

        // Liveness: verifica se o processo está responsivo
        // (o health check padrão retorna Healthy sem dependência de infra)
        healthBuilder.AddCheck(
            "liveness",
            () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(),
            tags: ["live"]);

        return services;
    }
}

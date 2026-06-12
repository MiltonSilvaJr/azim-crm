using AuditLog.Application.Abstractions;
using AuditLog.Domain.Abstractions;
using AuditLog.Domain.Repositories;
using AuditLog.Domain.Services;
using AuditLog.Infrastructure.Clock;
using AuditLog.Infrastructure.HealthChecks;
using AuditLog.Infrastructure.Observability;
using AuditLog.Infrastructure.Persistence;
using AuditLog.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuditLog.Infrastructure;

/// <summary>
/// Extensões de injeção de dependência para o projeto <c>AuditLog.Infrastructure</c>.
/// Registra persistência (EF Core, repositório), políticas de PII, métricas OpenTelemetry
/// e health check de capacidade de INSERT (design §11, RNF-004, RNF-006).
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adiciona os serviços da camada Infrastructure ao container de DI.
    /// </summary>
    /// <param name="services">Container de serviços.</param>
    /// <param name="connectionString">Connection string do PostgreSQL.</param>
    /// <returns>O mesmo <see cref="IServiceCollection"/> para encadeamento.</returns>
    public static IServiceCollection AddAuditLogInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // EF Core — PostgreSQL
        services.AddDbContext<AuditLogDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString);
        });

        // Repositório
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();

        // IClock — relógio do servidor (UTC)
        services.AddSingleton<IClock, SystemClock>();

        // PiiFieldPolicy — configuração padrão (campos de Contact como PII)
        services.AddSingleton<IPiiFieldPolicy, PiiFieldPolicy>();

        // PiiMasker — serviço de domínio puro, injeta IPiiFieldPolicy
        services.AddSingleton<PiiMasker>();

        // IAuditMetrics — implementação OpenTelemetry com System.Diagnostics.Metrics (design §11.2, Wave 6)
        services.AddSingleton<IAuditMetrics, OtelAuditMetrics>();

        // Health check de capacidade de INSERT em audit_logs (design §11.5, RNF-006)
        services.AddHealthChecks()
            .AddCheck<AuditInsertCapabilityHealthCheck>(
                name: "audit-insert-capability",
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                tags: ["audit", "database"]);

        return services;
    }
}

/// <summary>
/// Implementação NoOp de <see cref="IAuditMetrics"/>.
/// Usada em contextos onde a instrumentação OpenTelemetry não está disponível.
/// </summary>
internal sealed class NoOpAuditMetrics : IAuditMetrics
{
    public void IncrementEventsReceived() { }
    public void IncrementInsertFailures() { }
    public void IncrementQueryWithoutTenantContext() { }
    public void RecordInsertLatency(double seconds) { }
    public void IncrementPiiMaskingApplied() { }
}

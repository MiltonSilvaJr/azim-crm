using AuditLog.Application.Abstractions;
using AuditLog.Domain.Abstractions;
using AuditLog.Domain.Repositories;
using AuditLog.Domain.Services;
using AuditLog.Infrastructure.Clock;
using AuditLog.Infrastructure.Persistence;
using AuditLog.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuditLog.Infrastructure;

/// <summary>
/// Extensões de injeção de dependência para o projeto <c>AuditLog.Infrastructure</c>.
/// Registra persistência (EF Core, repositório), políticas de PII e IAuditMetrics (NoOp).
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

        // IAuditMetrics — implementação NoOp (substituída por OpenTelemetry na Wave 6)
        services.AddSingleton<IAuditMetrics, NoOpAuditMetrics>();

        return services;
    }
}

/// <summary>
/// Implementação NoOp de <see cref="IAuditMetrics"/>.
/// Usada no MVP enquanto a integração com OpenTelemetry/GCP não está configurada (Wave 6).
/// </summary>
internal sealed class NoOpAuditMetrics : IAuditMetrics
{
    public void IncrementEventsReceived() { }
    public void IncrementInsertFailures() { }
    public void IncrementQueryWithoutTenantContext() { }
}

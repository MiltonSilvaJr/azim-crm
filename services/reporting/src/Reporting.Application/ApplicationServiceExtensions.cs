using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Dispatching;
using Reporting.Application.Observability;
using Reporting.Application.Policies;
using Reporting.Application.Ports;
using Reporting.Application.Queries.Export;

namespace Reporting.Application;

/// <summary>
/// Extensões de registro de serviços da camada Application no contêiner de DI.
///
/// Registra:
/// <list type="bullet">
///   <item><description><see cref="CsvReportWriter"/> como <see cref="ICsvReportWriter"/> — implementação pura (sem IO).</description></item>
///   <item><description><see cref="PiiMinimizationPolicy"/> — política de minimização de PII (DD-008).</description></item>
/// </list>
///
/// Os handlers MediatR são registrados via <c>AddMediatR</c> no host — não duplicar aqui.
///
/// Mapeia: TASK-21, design §3, §5, DD-003.
/// </summary>
public static class ApplicationServiceExtensions
{
    /// <summary>
    /// Registra todos os serviços da camada Application que exigem registro explícito.
    /// </summary>
    /// <param name="services">Coleção de serviços do DI container.</param>
    /// <returns>A mesma coleção para encadeamento.</returns>
    public static IServiceCollection AddReportingApplication(this IServiceCollection services)
    {
        // CsvReportWriter: implementação pura (sem IO) do ICsvReportWriter (TASK-11, design §5.2)
        services.AddSingleton<ICsvReportWriter, CsvReportWriter>();

        // PiiMinimizationPolicy: política de minimização de PII para ranking (DD-008)
        services.AddSingleton<PiiMinimizationPolicy>();

        // ReportDispatcher: fachada que isola o controller de tipos do Domain (design §3, TASK-21)
        services.AddScoped<IReportDispatcher, ReportDispatcher>();

        // ReportingMetrics: métricas do módulo via System.Diagnostics.Metrics (design §11, RNF 6.2)
        // Singleton: Meter é thread-safe e deve ser compartilhado.
        services.AddSingleton<ReportingMetrics>();

        return services;
    }
}

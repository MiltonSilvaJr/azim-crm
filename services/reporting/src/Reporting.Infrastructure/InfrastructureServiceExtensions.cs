using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reporting.Application.Ports;
using Reporting.Infrastructure.Persistence;
using Reporting.Infrastructure.Storage;

namespace Reporting.Infrastructure;

/// <summary>
/// Extensões de registro de serviços da camada Infrastructure no contêiner de DI.
///
/// Registra:
/// <list type="bullet">
///   <item><description><see cref="TenantConnectionInterceptor"/> — interceptor de RLS (ADR-0001).</description></item>
///   <item><description><see cref="ReportingReadRepository"/> — repositório de leitura Dapper.</description></item>
///   <item><description><see cref="GcsCsvStorage"/> ou <see cref="InMemoryCsvStorage"/> — storage de CSV.</description></item>
///   <item><description><see cref="GcsOptions"/> — configuração do GCS via Options Pattern.</description></item>
/// </list>
///
/// Mapeia: TASK-13, TASK-19, design §3, §6.4, ADR-0001.
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Registra todos os serviços de infraestrutura do módulo reporting.
    /// </summary>
    /// <param name="services">Coleção de serviços do DI container.</param>
    /// <param name="connectionString">Connection string do PostgreSQL (do Secret Manager / env var).</param>
    /// <param name="configureGcs">Ação opcional de configuração do GCS.</param>
    /// <param name="useInMemoryStorage">
    ///   Se <c>true</c>, registra <see cref="InMemoryCsvStorage"/> em vez de <see cref="GcsCsvStorage"/>.
    ///   Use em testes de integração e desenvolvimento local.
    /// </param>
    public static IServiceCollection AddReportingInfrastructure(
        this IServiceCollection services,
        string connectionString,
        Action<GcsOptions>? configureGcs = null,
        bool useInMemoryStorage = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // Opções do GCS via Options Pattern
        var gcsBuilder = services.AddOptions<GcsOptions>();
        if (configureGcs is not null)
        {
            gcsBuilder.Configure(configureGcs);
        }

        // Interceptor de tenant (ADR-0001, DD-005)
        services.AddSingleton<TenantConnectionInterceptor>();

        // Repositório de leitura Dapper
        services.AddScoped<IReportingReadRepository>(sp =>
            new ReportingReadRepository(
                connectionString,
                sp.GetRequiredService<TenantConnectionInterceptor>(),
                sp.GetRequiredService<ILogger<ReportingReadRepository>>()));

        // Storage de CSV
        if (useInMemoryStorage)
        {
            services.AddSingleton<ICsvStorage>(new InMemoryCsvStorage());
        }
        else
        {
            services.AddSingleton<ICsvStorage, GcsCsvStorage>();
        }

        return services;
    }
}

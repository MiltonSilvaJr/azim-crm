using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Reporting.Infrastructure.Storage;

namespace Reporting.Infrastructure.HealthChecks;

/// <summary>
/// Health check de disponibilidade do Cloud Storage (GCS).
///
/// Verifica readiness do GCS tentando uma operação de metadados no bucket configurado.
/// Usa <see cref="ICsvStorageHealthProbe"/> (implementado pelo <see cref="GcsCsvStorage"/>
/// ou <see cref="InMemoryCsvStorage"/>) para verificar disponibilidade sem fazer upload real.
///
/// Falha isolada de GCS retorna <see cref="HealthStatus.Degraded"/> — o relatório pode
/// ser gerado mas o export estará indisponível (RNF 7, Tier 2 — não impede /health/live).
///
/// Nunca registra credenciais, keys ou URLs assinadas em logs (RNF 4.2).
///
/// Mapeia: TASK-24, design §11, RNF 7, TRD §11, ADR-0001.
/// </summary>
public sealed class GcsHealthCheck : IHealthCheck
{
    private readonly ICsvStorageHealthProbe _probe;
    private readonly ILogger<GcsHealthCheck> _logger;

    /// <summary>Inicializa o health check com o probe de storage.</summary>
    public GcsHealthCheck(ICsvStorageHealthProbe probe, ILogger<GcsHealthCheck> logger)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(logger);
        _probe  = probe;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isAvailable = await _probe.IsAvailableAsync(cancellationToken);

            return isAvailable
                ? HealthCheckResult.Healthy("Cloud Storage (GCS) acessível.")
                : HealthCheckResult.Degraded("Cloud Storage (GCS) com resposta inesperada.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Nunca loga credenciais ou URL do bucket — apenas o tipo de erro (RNF 4.2)
            _logger.LogWarning("Health check GCS: falha de conectividade ({ErrorType})", ex.GetType().Name);

            // Degraded (não Unhealthy): GCS indisponível não impede leitura de relatórios;
            // apenas o export estará indisponível (RNF 7, Tier 2).
            return HealthCheckResult.Degraded(
                description: "Cloud Storage (GCS) inacessível. Export indisponível.",
                exception: null,
                data: new Dictionary<string, object> { ["error_type"] = ex.GetType().Name });
        }
    }
}

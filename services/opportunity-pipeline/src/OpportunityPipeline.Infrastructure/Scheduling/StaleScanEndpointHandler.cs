using Microsoft.Extensions.Logging;
using OpportunityPipeline.Application.Opportunities.Queries;
using OpportunityPipeline.Application.Opportunities.Services;
using OpportunityPipeline.Domain.Opportunities.Ports;

namespace OpportunityPipeline.Infrastructure.Scheduling;

/// <summary>
/// Handler para o endpoint interno POST /internal/stale-scan.
/// Disparado pelo Cloud Scheduler — aciona StagnationDetectionService para cada tenant/BU.
/// Idempotente: reexecuções não duplicam OpportunityStale (PBT-09, RNF 9, DD-005).
/// Mapeia: design §5.3, §6.5, TASK-18.
/// </summary>
public sealed class StaleScanEndpointHandler(
    StagnationDetectionService stagnationService,
    IClock clock,
    ILogger<StaleScanEndpointHandler> logger)
{
    /// <summary>
    /// Executa o scan de estagnação para o tenant e BU informados.
    /// Retorna o número de oportunidades marcadas como estagnadas nesta execução.
    /// </summary>
    /// <param name="tenantId">Tenant alvo.</param>
    /// <param name="buId">BU alva.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Contagem de oportunidades recém-sinalizadas como estagnadas.</returns>
    public async Task<StaleScanResult> HandleAsync(
        Guid tenantId,
        Guid buId,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var detectionPeriod = DateOnly.FromDateTime(now.DateTime).ToString("yyyy-MM-dd");

        logger.LogInformation(
            "StaleScanEndpointHandler: iniciando scan tenant_id={TenantId}, bu_id={BuId}, period={Period}.",
            tenantId, buId, detectionPeriod);

        var count = await stagnationService
            .DetectAsync(tenantId, buId, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "StaleScanEndpointHandler: {Count} oportunidade(s) marcadas como estagnadas. Period={Period}.",
            count, detectionPeriod);

        return new StaleScanResult(tenantId, buId, detectionPeriod, count, now);
    }
}

/// <summary>Resultado do scan de estagnação.</summary>
public sealed record StaleScanResult(
    Guid TenantId,
    Guid BuId,
    string DetectionPeriod,
    int MarkedStaleCount,
    DateTimeOffset ExecutedAt);

using Microsoft.Extensions.Logging;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Opportunities.Queries;
using OpportunityPipeline.Domain.Opportunities.Ports;
using OpportunityPipeline.Domain.Opportunities.Repositories;
using OpportunityPipeline.Domain.Opportunities.Services;

namespace OpportunityPipeline.Application.Opportunities.Services;

/// <summary>
/// Application Service para detecção de oportunidades estagnadas.
/// Idempotente por (tenant_id, opportunity_id, detection_period).
/// Para cada oportunidade aberta, consulta last_activity_at via IActivityReadPort,
/// aplica StagnationSpecification e chama MarkStale() se necessário.
/// Registra em stale_detection_runs para garantir que cada oportunidade
/// receba no máximo 1 OpportunityStale por período.
/// Mapeia: Req 17, RNF 9, PBT-09, design §5.3, TASK-12 ST-07.
/// </summary>
public sealed class StagnationDetectionService(
    IOpportunityRepository repository,
    IOpportunityQueryRepository queryRepo,
    IActivityReadPort activityPort,
    IStaleDetectionRunRepository detectionRunRepo,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<StagnationDetectionService> logger)
{
    /// <summary>
    /// Executa a detecção de estagnação para todas as oportunidades abertas de um tenant/BU.
    /// Idempotente: re-execuções no mesmo detection_period não geram duplicatas.
    /// </summary>
    /// <param name="tenantId">Tenant alvo.</param>
    /// <param name="buId">Business Unit alva.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Número de oportunidades marcadas como estagnadas nesta execução.</returns>
    public async Task<int> DetectAsync(
        Guid tenantId,
        Guid buId,
        CancellationToken cancellationToken = default)
    {
        // detection_period = YYYY-MM (mensal — 1 marcação por oportunidade por mês)
        var detectionPeriod = clock.Today.ToString("yyyy-MM");
        var now = clock.UtcNow;
        var markedCount = 0;

        var openIds = await queryRepo.ListOpenOpportunityIdsAsync(tenantId, buId, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "StagnationDetectionService: {Count} oportunidades abertas para tenant={TenantId} bu={BuId} period={Period}",
            openIds.Count, tenantId, buId, detectionPeriod);

        foreach (var opportunityId in openIds)
        {
            try
            {
                await ProcessOpportunityAsync(tenantId, opportunityId, detectionPeriod, now, cancellationToken).ConfigureAwait(false);
                markedCount++;
            }
            catch (Exception ex)
            {
                // Falha em uma oportunidade não deve parar o loop — degração graciosa
                logger.LogWarning(
                    ex,
                    "StagnationDetectionService: falha ao processar oportunidade {OpportunityId}",
                    opportunityId);
            }
        }

        return markedCount;
    }

    private async Task ProcessOpportunityAsync(
        Guid tenantId,
        Guid opportunityId,
        string detectionPeriod,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Idempotência: verifica se já existe run para este período
        var alreadyRan = await detectionRunRepo.ExistsAsync(tenantId, opportunityId, detectionPeriod, cancellationToken).ConfigureAwait(false);
        if (alreadyRan)
        {
            logger.LogDebug(
                "StagnationDetectionService: oportunidade {OpportunityId} já processada no período {Period} — ignorando",
                opportunityId, detectionPeriod);
            return;
        }

        // Consulta last_activity_at (degradação graciosa: null se indisponível)
        var lastActivityAt = await activityPort.GetLastActivityAtAsync(tenantId, opportunityId, cancellationToken).ConfigureAwait(false);
        if (lastActivityAt is null)
        {
            logger.LogDebug(
                "StagnationDetectionService: last_activity_at indisponível para {OpportunityId} — usando CreatedAt como fallback não aplicável",
                opportunityId);
            return;
        }

        // Carrega agregado para aplicar especificação e MarkStale
        var opportunity = await repository.GetByIdAsync(opportunityId, tenantId, cancellationToken).ConfigureAwait(false);
        if (opportunity is null) return;

        var detectionPeriodDate = DateOnly.ParseExact(detectionPeriod, "yyyy-MM");

        var isStagnant = StagnationSpecification.IsSatisfiedBy(
            opportunity.StageCategory,
            lastActivityAt.Value,
            now);

        if (isStagnant && !opportunity.IsStale)
        {
            opportunity.MarkStale(lastActivityAt.Value, now, detectionPeriodDate);
            await repository.SaveAsync(opportunity, cancellationToken).ConfigureAwait(false);
            unitOfWork.AddDomainEvents(opportunity.DomainEvents);
            opportunity.ClearDomainEvents();

            logger.LogInformation(
                "StagnationDetectionService: oportunidade {OpportunityId} marcada como estagnada",
                opportunityId);
        }

        // Registra run para este período (garante idempotência futura)
        await detectionRunRepo.RegisterAsync(tenantId, opportunityId, detectionPeriod, now, cancellationToken).ConfigureAwait(false);
    }
}

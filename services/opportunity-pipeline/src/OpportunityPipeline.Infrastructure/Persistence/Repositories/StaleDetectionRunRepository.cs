using Microsoft.EntityFrameworkCore;
using OpportunityPipeline.Application.Opportunities.Queries;

namespace OpportunityPipeline.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repositório EF Core para registros de detecção de estagnação.
/// PK: (tenant_id, opportunity_id, detection_period) — idempotência garantida (RNF 9, PBT-09).
/// Mapeia: design §7.4, TASK-18.
/// </summary>
public sealed class StaleDetectionRunRepository(OpportunityDbContext context)
    : IStaleDetectionRunRepository
{
    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(
        Guid tenantId,
        Guid opportunityId,
        string detectionPeriod,
        CancellationToken cancellationToken = default)
    {
        return await context.StaleDetectionRuns
            .AsNoTracking()
            .AnyAsync(
                r => r.TenantId == tenantId
                     && r.OpportunityId == opportunityId
                     && r.DetectionPeriod == detectionPeriod,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RegisterAsync(
        Guid tenantId,
        Guid opportunityId,
        string detectionPeriod,
        DateTimeOffset detectedAt,
        CancellationToken cancellationToken = default)
    {
        // INSERT idempotente — ignora conflito de PK (mesmo resultado)
        var run = new StaleDetectionRun
        {
            TenantId = tenantId,
            OpportunityId = opportunityId,
            DetectionPeriod = detectionPeriod,
            DetectedAt = detectedAt
        };

        context.StaleDetectionRuns.Add(run);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

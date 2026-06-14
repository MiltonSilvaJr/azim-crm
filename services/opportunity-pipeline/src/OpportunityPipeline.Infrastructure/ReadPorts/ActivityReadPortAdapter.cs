using Microsoft.Extensions.Logging;
using OpportunityPipeline.Domain.Opportunities.Ports;
using System.Net.Http.Json;

namespace OpportunityPipeline.Infrastructure.ReadPorts;

/// <summary>
/// Adapter HTTP para IActivityReadPort — upstream conformist (design §6.4).
/// Degradação graciosa: se activity-management indisponível, retorna null
/// (o scan de estagnação é adiado, não falha com 500 — DD-005).
/// Mapeia: Req 17, design §6.4, TASK-17.
/// </summary>
public sealed class ActivityReadPortAdapter(
    HttpClient httpClient,
    ILogger<ActivityReadPortAdapter> logger)
    : IActivityReadPort
{
    /// <inheritdoc/>
    public async Task<DateTimeOffset?> GetLastActivityAtAsync(
        Guid tenantId,
        Guid opportunityId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dto = await httpClient
                .GetFromJsonAsync<LastActivityDto>(
                    $"internal/activities/tenants/{tenantId}/opportunities/{opportunityId}/last",
                    cancellationToken)
                .ConfigureAwait(false);

            return dto?.LastActivityAt;
        }
        catch (Exception ex)
        {
            // Degradação graciosa — não falha o scan, apenas adia (DD-005)
            logger.LogWarning(ex,
                "ActivityAdapter: falha ao obter last_activity_at para opportunity_id={OpportunityId}. Estagnação adiada.",
                opportunityId);
            return null;
        }
    }

    private sealed record LastActivityDto(DateTimeOffset? LastActivityAt);
}

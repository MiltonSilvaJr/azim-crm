using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OpportunityPipeline.Domain.Opportunities.Ports;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;
using System.Net.Http.Json;

namespace OpportunityPipeline.Infrastructure.ReadPorts;

/// <summary>
/// Adapter HTTP para IPartnerReadPort — upstream conformist (design §6.4).
/// Cache de CommissionDefaults com TTL 60s por tenant/partner (design §6.2).
/// Resiliência via Polly (TASK-17).
/// Mapeia: Req 11, design §6.4, TASK-17.
/// </summary>
public sealed class PartnerReadPortAdapter(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<PartnerReadPortAdapter> logger)
    : IPartnerReadPort
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    /// <inheritdoc/>
    public async Task<bool> PartnerExistsAsync(
        Guid tenantId,
        Guid partnerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient
                .GetAsync(
                    $"internal/partners/tenants/{tenantId}/partners/{partnerId}",
                    cancellationToken)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "PartnerAdapter: falha ao verificar existência de partner_id={PartnerId}.",
                partnerId);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<CommissionDefaults?> GetCommissionDefaultsAsync(
        Guid tenantId,
        Guid partnerId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"commission_defaults:{tenantId}:{partnerId}";

        if (cache.TryGetValue(cacheKey, out CommissionDefaults? cached))
            return cached;

        try
        {
            var dto = await httpClient
                .GetFromJsonAsync<CommissionDefaultsDto>(
                    $"internal/partners/tenants/{tenantId}/partners/{partnerId}/commission-defaults",
                    cancellationToken)
                .ConfigureAwait(false);

            if (dto is null) return null;

            var defaults = new CommissionDefaults(dto.PctSetup, dto.PctRecorrente);
            cache.Set(cacheKey, defaults, CacheTtl);
            return defaults;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "PartnerAdapter: falha ao obter CommissionDefaults partner_id={PartnerId}.",
                partnerId);
            return null;
        }
    }

    private sealed record CommissionDefaultsDto(decimal PctSetup, decimal PctRecorrente);
}

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OpportunityPipeline.Domain.Opportunities.Ports;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;
using System.Net.Http.Json;
using System.Text.Json;

namespace OpportunityPipeline.Infrastructure.ReadPorts;

/// <summary>
/// Adapter HTTP para IOrganizationReadPort — upstream conformist (design §6.4).
/// Resiliência: timeout 2s, 3 retries com backoff, circuit breaker via Polly (TASK-17).
/// Cache de StageRef/OriginChannelRef com TTL 60s por tenant/BU (design §6.2).
/// Mapeia: Req 2, Req 4, Req 5, design §6.4, TASK-17.
/// </summary>
public sealed class OrganizationReadPortAdapter(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<OrganizationReadPortAdapter> logger)
    : IOrganizationReadPort
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    /// <inheritdoc/>
    public async Task<bool> ValidateOwnerMembershipAsync(
        Guid tenantId,
        Guid buId,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient
                .GetAsync(
                    $"internal/organization/tenants/{tenantId}/bus/{buId}/members/{ownerId}",
                    cancellationToken)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "OrganizationAdapter: falha ao validar membership owner_id={OwnerId} bu_id={BuId}.",
                ownerId, buId);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<StageRef?> GetStageRefAsync(
        Guid tenantId,
        Guid stageId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"stage:{tenantId}:{stageId}";

        if (cache.TryGetValue(cacheKey, out StageRef? cached))
            return cached;

        try
        {
            var dto = await httpClient
                .GetFromJsonAsync<StageRefDto>(
                    $"internal/organization/tenants/{tenantId}/stages/{stageId}",
                    cancellationToken)
                .ConfigureAwait(false);

            if (dto is null) return null;

            var stageRef = new StageRef(
                dto.Id,
                dto.Name,
                Enum.Parse<StageCategory>(dto.Category, ignoreCase: true),
                dto.DefaultProbability,
                dto.Order);

            cache.Set(cacheKey, stageRef, CacheTtl);
            return stageRef;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "OrganizationAdapter: falha ao obter StageRef stage_id={StageId}.",
                stageId);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<OriginChannelRef?> GetOriginChannelRefAsync(
        Guid tenantId,
        Guid originChannelId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"origin:{tenantId}:{originChannelId}";

        if (cache.TryGetValue(cacheKey, out OriginChannelRef? cached))
            return cached;

        try
        {
            var dto = await httpClient
                .GetFromJsonAsync<OriginChannelDto>(
                    $"internal/organization/tenants/{tenantId}/origin-channels/{originChannelId}",
                    cancellationToken)
                .ConfigureAwait(false);

            if (dto is null) return null;

            var channelRef = new OriginChannelRef(dto.Id, dto.Name, dto.IsPartnerChannel);
            cache.Set(cacheKey, channelRef, CacheTtl);
            return channelRef;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "OrganizationAdapter: falha ao obter OriginChannelRef channel_id={ChannelId}.",
                originChannelId);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<LossReasonRef?> GetLossReasonRefAsync(
        Guid tenantId,
        Guid lossReasonId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"loss_reason:{tenantId}:{lossReasonId}";

        if (cache.TryGetValue(cacheKey, out LossReasonRef? cached))
            return cached;

        try
        {
            var dto = await httpClient
                .GetFromJsonAsync<LossReasonDto>(
                    $"internal/organization/tenants/{tenantId}/loss-reasons/{lossReasonId}",
                    cancellationToken)
                .ConfigureAwait(false);

            if (dto is null) return null;

            var lossRef = new LossReasonRef(dto.Id, dto.Name);
            cache.Set(cacheKey, lossRef, CacheTtl);
            return lossRef;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "OrganizationAdapter: falha ao obter LossReasonRef loss_reason_id={LossReasonId}.",
                lossReasonId);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<int> GetPropostaEnviadaOrderAsync(
        Guid tenantId,
        Guid buId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"proposta_order:{tenantId}:{buId}";

        if (cache.TryGetValue(cacheKey, out int cached))
            return cached;

        try
        {
            var result = await httpClient
                .GetFromJsonAsync<PropostaOrderDto>(
                    $"internal/organization/tenants/{tenantId}/bus/{buId}/proposta-enviada-order",
                    cancellationToken)
                .ConfigureAwait(false);

            var order = result?.Order ?? int.MaxValue;
            cache.Set(cacheKey, order, CacheTtl);
            return order;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "OrganizationAdapter: falha ao obter PropostaEnviadaOrder bu_id={BuId}.",
                buId);
            return int.MaxValue; // fallback conservador: não bloqueia a movimentação
        }
    }

    // DTOs internos de deserialização (sem PII, apenas IDs e metadados de configuração)
    private sealed record StageRefDto(Guid Id, string Name, string Category, int DefaultProbability, int Order);
    private sealed record OriginChannelDto(Guid Id, string Name, bool IsPartnerChannel);
    private sealed record LossReasonDto(Guid Id, string Name);
    private sealed record PropostaOrderDto(int Order);
}

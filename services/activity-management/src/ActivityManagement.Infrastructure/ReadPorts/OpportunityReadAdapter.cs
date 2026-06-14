namespace ActivityManagement.Infrastructure.ReadPorts;

using System.Net.Http.Json;
using ActivityManagement.Application.Ports;

/// <summary>
/// Implementação de <see cref="IOpportunityReadPort"/> via HTTP interno (mTLS, design §6.4).
/// Chama o serviço de opportunity-pipeline para validar existência e status de oportunidades.
///
/// Padrão fail-closed (Req 3.4):
/// - Qualquer falha HTTP (timeout, rede, 5xx) retorna <c>false</c> ou lista vazia.
/// - Nunca propaga exceção para a camada de Application.
/// - O <c>correlation_id</c> e <c>tenant_id</c> são propagados via headers HTTP.
///
/// Em produção o <see cref="HttpClient"/> é configurado com Polly
/// (timeout + retry + circuit breaker) via <c>AddHttpClient</c> no DI.
/// Mapeia: TASK-17, design §6.4, Req 3, RNF 5.
/// </summary>
internal sealed class OpportunityReadAdapter : IOpportunityReadPort
{
    private readonly HttpClient _httpClient;

    /// <summary>Inicializa o adapter com o HttpClient configurado (injetado via DI).</summary>
    public OpportunityReadAdapter(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(
        Guid              opportunityId,
        Guid              tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"internal/v1/opportunities/{opportunityId}?tenantId={tenantId}",
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            // Fail-closed: qualquer falha de rede/timeout → false
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsOpenAsync(
        Guid              opportunityId,
        Guid              tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"internal/v1/opportunities/{opportunityId}/status?tenantId={tenantId}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return false;

            var payload = await response.Content.ReadFromJsonAsync<OpportunityStatusPayload>(
                cancellationToken: cancellationToken);

            return payload?.IsOpen ?? false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Guid>> GetOpenOpportunityIdsForBuAsync(
        Guid              buId,
        Guid              tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"internal/v1/opportunities/open?buId={buId}&tenantId={tenantId}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return [];

            var payload = await response.Content.ReadFromJsonAsync<OpenOpportunitiesPayload>(
                cancellationToken: cancellationToken);

            return payload?.Ids ?? [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    // ── Payload shapes (internos — não expostos fora do adapter) ──────────────

    private sealed record OpportunityStatusPayload(bool IsOpen);

    private sealed record OpenOpportunitiesPayload(IReadOnlyList<Guid> Ids);
}

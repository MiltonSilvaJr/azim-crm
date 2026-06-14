using System.Net.Http.Json;
using Digest.Application.Models;
using Digest.Application.Ports;
using Microsoft.Extensions.Logging;
using Polly;

namespace Digest.Infrastructure.Adapters;

/// <summary>
/// Adaptador de <see cref="IOpportunityReadPort"/> com resiliência Polly (TASK-19).
/// Nome: PipelineReadAdapter (evita prefixo "Opportunity*" proibido pela Architecture rule — Req 6.2).
/// No MVP monolítico: leitura direta via HTTP interno do módulo opportunity-pipeline.
/// Timeout 10s, retry exponencial x3, circuit breaker (design §6.4, RNF 5).
/// </summary>
public sealed class PipelineReadAdapter : IOpportunityReadPort
{
    private readonly HttpClient _http;
    private readonly ResiliencePipeline<IReadOnlyList<OpportunityItem>> _pipeline;

    /// <summary>
    /// Constrói o adaptador com o HttpClient nomeado e o pipeline Polly.
    /// </summary>
    public PipelineReadAdapter(HttpClient http, ILogger<PipelineReadAdapter> logger)
    {
        _http = http;
        _pipeline = ResiliencePipelineFactory.CreateForList<OpportunityItem>(
            nameof(PipelineReadAdapter), logger);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<OpportunityItem>> GetStaleOpportunitiesAsync(
        Guid tenantId,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithResilienceAsync(
            $"/internal/opportunities/stale?tenantId={tenantId}&ownerId={ownerId}",
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<OpportunityItem>> GetOverdueClosingsAsync(
        Guid tenantId,
        Guid ownerId,
        DateOnly referenceDate,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithResilienceAsync(
            $"/internal/opportunities/overdue-closings?tenantId={tenantId}&ownerId={ownerId}&referenceDate={referenceDate:yyyy-MM-dd}",
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<OpportunityItem>> GetWeightedPipelineAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithResilienceAsync(
            $"/internal/opportunities/pipeline?tenantId={tenantId}",
            cancellationToken);
    }

    private async Task<IReadOnlyList<OpportunityItem>> ExecuteWithResilienceAsync(
        string url,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _pipeline.ExecuteAsync(async ct =>
            {
                var items = await _http.GetFromJsonAsync<List<OpportunityItem>>(url, ct);
                return (IReadOnlyList<OpportunityItem>)(items ?? []);
            }, cancellationToken) ?? Array.Empty<OpportunityItem>();
        }
        catch (Exception ex) when (ex is HttpRequestException or Polly.Timeout.TimeoutRejectedException or Polly.CircuitBreaker.BrokenCircuitException)
        {
            // Degradação graciosa: retorna lista vazia quando fonte irrecuperável (RNF 5.4)
            return Array.Empty<OpportunityItem>();
        }
    }
}

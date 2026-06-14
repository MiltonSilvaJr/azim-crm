using System.Net.Http.Json;
using Digest.Application.Models;
using Digest.Application.Ports;
using Microsoft.Extensions.Logging;
using Polly;

namespace Digest.Infrastructure.Adapters;

/// <summary>
/// Adaptador de <see cref="IActivityReadPort"/> com resiliência Polly (TASK-19).
/// No MVP monolítico: leitura direta via HTTP interno do módulo activity-management.
/// Timeout 10s, retry exponencial x3, circuit breaker (design §6.4, RNF 5).
/// </summary>
public sealed class ActivityReadAdapter : IActivityReadPort
{
    private readonly HttpClient _http;
    private readonly ResiliencePipeline<IReadOnlyList<ActivityItem>> _pipeline;

    /// <summary>
    /// Constrói o adaptador com o HttpClient nomeado (IHttpClientFactory) e o pipeline Polly.
    /// </summary>
    public ActivityReadAdapter(HttpClient http, ILogger<ActivityReadAdapter> logger)
    {
        _http = http;
        _pipeline = ResiliencePipelineFactory.CreateForList<ActivityItem>(
            nameof(ActivityReadAdapter), logger);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ActivityItem>> GetOverdueActivitiesAsync(
        Guid tenantId,
        Guid ownerId,
        DateOnly referenceDate,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithResilienceAsync(
            $"/internal/activities/overdue?tenantId={tenantId}&ownerId={ownerId}&referenceDate={referenceDate:yyyy-MM-dd}",
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ActivityItem>> GetTodayActivitiesAsync(
        Guid tenantId,
        Guid ownerId,
        DateOnly referenceDate,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithResilienceAsync(
            $"/internal/activities/today?tenantId={tenantId}&ownerId={ownerId}&referenceDate={referenceDate:yyyy-MM-dd}",
            cancellationToken);
    }

    private async Task<IReadOnlyList<ActivityItem>> ExecuteWithResilienceAsync(
        string url,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _pipeline.ExecuteAsync(async ct =>
            {
                var items = await _http.GetFromJsonAsync<List<ActivityItem>>(url, ct);
                return (IReadOnlyList<ActivityItem>)(items ?? []);
            }, cancellationToken) ?? Array.Empty<ActivityItem>();
        }
        catch (Exception ex) when (ex is HttpRequestException or Polly.Timeout.TimeoutRejectedException or Polly.CircuitBreaker.BrokenCircuitException)
        {
            // Degradação graciosa: retorna lista vazia quando fonte irrecuperável (RNF 5.4)
            return Array.Empty<ActivityItem>();
        }
    }
}

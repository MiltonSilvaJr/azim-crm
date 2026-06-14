using System.Net.Http.Json;
using Digest.Application.Models;
using Digest.Application.Ports;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Digest.Infrastructure.Adapters;

/// <summary>
/// Adaptador de <see cref="IForecastReadPort"/> com resiliência Polly (TASK-19).
/// No MVP monolítico: leitura direta via HTTP interno do módulo goal-forecast.
/// Retorna <see langword="null"/> quando o dado não existe ou a fonte está indisponível
/// (degradação graciosa — RNF 5.4, PBT-04).
/// </summary>
public sealed class ForecastReadAdapter : IForecastReadPort
{
    private readonly HttpClient _http;
    private readonly ResiliencePipeline<ForecastBlock?> _pipeline;
    private readonly ILogger<ForecastReadAdapter> _logger;

    /// <summary>
    /// Constrói o adaptador com o HttpClient nomeado e o pipeline Polly.
    /// </summary>
    public ForecastReadAdapter(HttpClient http, ILogger<ForecastReadAdapter> logger)
    {
        _http = http;
        _logger = logger;
        _pipeline = ResiliencePipelineFactory.Create<ForecastBlock?>(
            nameof(ForecastReadAdapter), logger);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Retorna <see langword="null"/> em caso de:
    /// - 404 Not Found (sem meta cadastrada para o período);
    /// - timeout após retentativas (degradação graciosa — RNF 5.4);
    /// - circuit breaker aberto (DIG-ERR-030).
    /// Nunca lança exceção para o caller (Req 5.4).
    /// </remarks>
    public async Task<ForecastBlock?> GetForecastBlockAsync(
        Guid tenantId,
        DateOnly referenceDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _pipeline.ExecuteAsync(async ct =>
            {
                var url = $"/internal/forecast?tenantId={tenantId}&referenceDate={referenceDate:yyyy-MM-dd}";
                var response = await _http.GetAsync(url, ct);

                // 404 = sem meta — retorna null (degradação graciosa, não erro)
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ForecastBlock>(ct);
            }, cancellationToken);
        }
        catch (TimeoutRejectedException ex)
        {
            // Timeout irrecuperável após retentativas — degradação graciosa (RNF 5.4)
            _logger.LogWarning(ex, "[ForecastReadAdapter] Timeout irrecuperável — retornando null (RNF 5.4)");
            return null;
        }
        catch (BrokenCircuitException ex)
        {
            // Circuit breaker aberto — degradação graciosa (DIG-ERR-030)
            _logger.LogWarning(ex, "[ForecastReadAdapter] Circuit breaker aberto — retornando null (DIG-ERR-030)");
            return null;
        }
        catch (HttpRequestException ex)
        {
            // Falha de rede irrecuperável — degradação graciosa (RNF 5.4)
            _logger.LogWarning(ex, "[ForecastReadAdapter] Falha HTTP irrecuperável — retornando null (RNF 5.4)");
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // CancellationToken interno do Polly — não é cancelamento externo
            return null;
        }
    }
}

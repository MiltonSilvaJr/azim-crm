using GoalForecast.Application.Ports;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Fallback;
using Polly.Timeout;

namespace GoalForecast.Infrastructure.Pipeline;

/// <summary>
/// Implementação concreta da porta <see cref="IPipelineForecastReader"/>.
/// Lê o read model ForecastView in-process do opportunity-pipeline (DD-005)
/// com resiliência obrigatória: timeout + circuit breaker (RNF 6, DD-007).
///
/// Contrato de resiliência:
/// <list type="bullet">
///   <item>Timeout ou exceção → retorna <see cref="ForecastViewResult.Unavailable"/> (never throws).</item>
///   <item>Circuit breaker aberto → retorna <see cref="ForecastViewResult.Unavailable"/> imediatamente.</item>
///   <item>Nunca escreve no pipeline (Req 8.3).</item>
/// </list>
///
/// Mapeia: Req 8, RNF 6, DD-005, DD-007, RISK-GOAL-01, design §6.4, TASK-19.
/// </summary>
public sealed class PipelineForecastReader : IPipelineForecastReader
{
    private readonly IForecastViewSource _source;
    private readonly ILogger<PipelineForecastReader> _logger;
    private readonly ResiliencePipeline<ForecastViewResult> _pipeline;

    /// <summary>
    /// Cria o reader com timeoutMs e parâmetros de circuit breaker configuráveis.
    /// Em produção, os valores são injetados via options (design §6.4).
    /// Em testes, os parâmetros são passados diretamente para controle fino.
    /// </summary>
    public PipelineForecastReader(
        IForecastViewSource source,
        ILogger<PipelineForecastReader> logger,
        int timeoutMs = 3_000,
        int failuresBeforeOpen = 5,
        int breakDurationMs = 30_000)
    {
        _source = source;
        _logger = logger;
        _pipeline = BuildResiliencePipeline(timeoutMs, failuresBeforeOpen, breakDurationMs);
    }

    /// <inheritdoc/>
    public async Task<ForecastViewResult> Read(
        ForecastViewQuery query,
        CancellationToken cancellationToken = default)
    {
        return await _pipeline.ExecuteAsync(
            async ct =>
            {
                var (won, forecast) = await _source.ReadAsync(query, ct);
                return new ForecastViewResult(won, forecast, Available: true);
            },
            cancellationToken);
    }

    private ResiliencePipeline<ForecastViewResult> BuildResiliencePipeline(
        int timeoutMs,
        int failuresBeforeOpen,
        int breakDurationMs)
    {
        return new ResiliencePipelineBuilder<ForecastViewResult>()
            // 1. Circuit breaker — deve vir antes do timeout para contabilizar falhas
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<ForecastViewResult>
            {
                FailureRatio = 1.0,
                MinimumThroughput = failuresBeforeOpen,
                SamplingDuration = TimeSpan.FromSeconds(60),
                BreakDuration = TimeSpan.FromMilliseconds(breakDurationMs),
                ShouldHandle = new PredicateBuilder<ForecastViewResult>()
                    .Handle<Exception>(),
                OnOpened = args =>
                {
                    _logger.LogWarning(
                        "PipelineForecastReader: circuit breaker aberto. Degradação ativa. " +
                        "Próxima tentativa em {BreakDuration}.",
                        args.BreakDuration);
                    return ValueTask.CompletedTask;
                }
            })
            // 2. Timeout — controla duração máxima da chamada à fonte
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromMilliseconds(timeoutMs),
                OnTimeout = args =>
                {
                    _logger.LogWarning(
                        "PipelineForecastReader: timeout após {Timeout}ms. Retornando degradação.",
                        timeoutMs);
                    return ValueTask.CompletedTask;
                }
            })
            // 3. Fallback universal — captura qualquer exceção e retorna Unavailable
            .AddFallback(new FallbackStrategyOptions<ForecastViewResult>
            {
                ShouldHandle = new PredicateBuilder<ForecastViewResult>()
                    .Handle<Exception>(),
                FallbackAction = args =>
                {
                    _logger.LogWarning(
                        args.Outcome.Exception,
                        "PipelineForecastReader: falha na leitura do pipeline. " +
                        "Retornando pipelineUnavailable=true. Tipo: {ExceptionType}.",
                        args.Outcome.Exception?.GetType().Name ?? "unknown");

                    return ValueTask.FromResult(Outcome.FromResult(ForecastViewResult.Unavailable));
                }
            })
            .Build();
    }
}

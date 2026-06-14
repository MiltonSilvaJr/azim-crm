using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace PartnerManagement.Infrastructure.ReadPorts;

/// <summary>
/// Fábrica do pipeline de resiliência Polly para o <see cref="PartnerCommissionReadAdapter"/>.
/// Encapsula: timeout por tentativa → retry com backoff exponencial → circuit breaker.
/// A ordem é: timeout (mais interno) → retry (envolvendo timeout) → circuit breaker (mais externo).
/// Mapeia: TASK-28, design §15, RISK-PM-06.
/// </summary>
internal static class CommissionReadResiliencePipeline
{
    /// <summary>
    /// Constrói o pipeline de resiliência com as opções fornecidas.
    /// </summary>
    /// <param name="options">Configuração de timeout, retry e circuit breaker.</param>
    /// <returns>Pipeline configurado para uso no adapter.</returns>
    public static ResiliencePipeline<HttpResponseMessage> Build(CommissionReadResilienceOptions options)
    {
        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            // 1. Circuit breaker — mais externo; abre após threshold de falhas consecutivas
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 1.0,
                MinimumThroughput = options.CircuitBreakerFailureThreshold,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(options.CircuitBreakerBreakDurationSeconds),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .Handle<TimeoutRejectedException>()
                    .HandleResult(r => !r.IsSuccessStatusCode)
            })
            // 2. Retry — backoff exponencial com jitter; envolve o timeout
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = options.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(options.RetryBaseDelaySeconds),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .Handle<TimeoutRejectedException>()
                    .HandleResult(r => !r.IsSuccessStatusCode)
            })
            // 3. Timeout por tentativa — mais interno; cancela chamadas lentas
            .AddTimeout(TimeSpan.FromSeconds(options.TimeoutSeconds))
            .Build();
    }
}

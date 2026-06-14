using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Digest.Infrastructure.Adapters;

/// <summary>
/// Factory de pipelines de resiliência Polly para as portas de leitura do digest (TASK-19).
/// Configuração: timeout 10s, retry com backoff exponencial (3 tentativas), circuit breaker (design §6.4, RNF 5).
/// </summary>
public static class ResiliencePipelineFactory
{
    /// <summary>
    /// Cria um pipeline de resiliência padrão para portas de leitura.
    /// Ordem das estratégias (de fora para dentro):
    /// 1. Timeout geral (10s)
    /// 2. Retry com backoff exponencial (3 tentativas, jitter)
    /// 3. Circuit breaker (5 falhas em 30s → aberto por 60s)
    /// </summary>
    /// <param name="portName">Nome da porta (usado em logs e métricas).</param>
    /// <param name="logger">Logger opcional para eventos de resiliência.</param>
    public static ResiliencePipeline<T> Create<T>(string portName, ILogger? logger = null)
        where T : class?
    {
        return new ResiliencePipelineBuilder<T>()
            // Timeout geral: cancela se demorar mais de 10s (RNF 5.1)
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(10),
                OnTimeout = args =>
                {
                    logger?.LogWarning(
                        "[{Port}] Timeout de 10s excedido (RNF 5.1). Tentativa: {Attempt}",
                        portName, args.Context.Properties.TryGetValue(new ResiliencePropertyKey<int>("attempt"), out var a) ? a : 0);
                    return ValueTask.CompletedTask;
                }
            })
            // Retry com backoff exponencial + jitter (RNF 5.2)
            .AddRetry(new RetryStrategyOptions<T>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<T>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
                    .Handle<OperationCanceledException>(),
                OnRetry = args =>
                {
                    logger?.LogWarning(
                        "[{Port}] Retentativa {Attempt} após {Delay:g}",
                        portName, args.AttemptNumber + 1, args.RetryDelay);
                    return ValueTask.CompletedTask;
                }
            })
            // Circuit breaker (DIG-ERR-030): abre após 5 falhas em 30s, fechado após 60s
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<T>
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(60),
                ShouldHandle = new PredicateBuilder<T>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
                    .Handle<OperationCanceledException>(),
                OnOpened = args =>
                {
                    logger?.LogError(
                        "[{Port}] Circuit breaker ABERTO — DIG-ERR-030. Duração: {Duration:g}",
                        portName, args.BreakDuration);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    logger?.LogInformation("[{Port}] Circuit breaker FECHADO — retomando operação", portName);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Cria um pipeline de resiliência para portas que retornam listas (nunca nulas).
    /// Em caso de falha irrecuperável, retorna lista vazia (degradação graciosa — RNF 5.4).
    /// </summary>
    public static ResiliencePipeline<IReadOnlyList<T>> CreateForList<T>(string portName, ILogger? logger = null)
    {
        return new ResiliencePipelineBuilder<IReadOnlyList<T>>()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(10)
            })
            .AddRetry(new RetryStrategyOptions<IReadOnlyList<T>>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<IReadOnlyList<T>>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
                    .Handle<OperationCanceledException>()
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<IReadOnlyList<T>>
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(60),
                ShouldHandle = new PredicateBuilder<IReadOnlyList<T>>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
                    .Handle<OperationCanceledException>(),
                OnOpened = args =>
                {
                    logger?.LogError(
                        "[{Port}] Circuit breaker ABERTO — DIG-ERR-030", portName);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }
}

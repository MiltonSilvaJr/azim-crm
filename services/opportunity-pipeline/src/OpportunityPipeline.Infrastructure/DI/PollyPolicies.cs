using Polly;
using Polly.Extensions.Http;

namespace OpportunityPipeline.Infrastructure.DI;

/// <summary>
/// Políticas Polly reutilizáveis para HttpClient com resiliência padrão.
/// Retry: 3 tentativas com backoff exponencial + jitter.
/// Circuit breaker: 5 falhas consecutivas abre o circuito por 30 s.
/// Mapeia: NFR-RES, design §6.4, TASK-20.
/// </summary>
public static class PollyPolicies
{
    /// <summary>
    /// Política de retry com backoff exponencial e jitter.
    /// 3 retentativas: 2 s, 4 s, 8 s (± jitter até 200 ms).
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, attempt))
                    + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 200)));
    }

    /// <summary>
    /// Política de circuit breaker.
    /// Abre após 5 falhas consecutivas; mantém aberto por 30 s.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));
    }
}

namespace NotificationDelivery.Application.Resilience;

/// <summary>
/// Opções de configuração do <see cref="ResilientEmailSender"/>, injetáveis via <c>IOptions&lt;T&gt;</c>.
///
/// Valores padrão alinhados ao TRD §17 e design §6.4 (Req 8, RNF 3, DD-004):
/// <list type="bullet">
///   <item><description>Timeout por tentativa: 10 s (RNF-3.1).</description></item>
///   <item><description>Máximo de tentativas: 5 (RNF-3.2).</description></item>
///   <item><description>Circuit breaker: abre após 5 falhas consecutivas (RNF-3.3).</description></item>
///   <item><description>Backoff: exponencial com jitter para evitar thundering herd.</description></item>
/// </list>
/// </summary>
public sealed class ResilientEmailSenderOptions
{
    /// <summary>
    /// Seção de configuração padrão no <c>appsettings.json</c>.
    /// Uso: <c>builder.Services.Configure&lt;ResilientEmailSenderOptions&gt;(config.GetSection(SectionName));</c>
    /// </summary>
    public const string SectionName = "NotificationDelivery:Resilience";

    /// <summary>
    /// Timeout por tentativa em segundos.
    /// Padrão: 10 s (TRD §17, RNF-3.1).
    /// </summary>
    public int TimeoutPerAttemptSeconds { get; init; } = 10;

    /// <summary>
    /// Número máximo de tentativas de retry após a primeira tentativa.
    /// Padrão: 5 (TRD §17, RNF-3.2).
    /// Total de tentativas = MaxRetryAttempts + 1.
    /// </summary>
    public int MaxRetryAttempts { get; init; } = 5;

    /// <summary>
    /// Número de falhas consecutivas de provedor que abre o circuit breaker.
    /// Padrão: 5 (TRD §17, RNF-3.3).
    /// <c>Bounced</c> e <c>Suppressed</c> não contam (Req 7.3).
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; init; } = 5;

    /// <summary>
    /// Duração da janela de abertura do circuit breaker em segundos.
    /// Padrão: 30 s — após este período, o breaker entra em half-open (RNF-3.3).
    /// </summary>
    public int CircuitBreakerBreakDurationSeconds { get; init; } = 30;

    /// <summary>
    /// Delay base do backoff exponencial em milissegundos.
    /// O delay real de cada tentativa é <c>BaseRetryDelayMs * 2^(tentativa-1)</c> + jitter opcional.
    /// Padrão: 1000 ms (1 s).
    /// </summary>
    public int BaseRetryDelayMs { get; init; } = 1_000;

    /// <summary>
    /// Habilita jitter no backoff exponencial para evitar thundering herd.
    /// Padrão: <c>true</c>.
    /// </summary>
    public bool UseJitter { get; init; } = true;
}

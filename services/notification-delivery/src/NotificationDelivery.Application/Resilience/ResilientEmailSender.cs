using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationDelivery.Application.Validation;
using NotificationDelivery.Contracts;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace NotificationDelivery.Application.Resilience;

/// <summary>
/// Implementação de <see cref="IEmailSender"/> que aplica resiliência (Polly v8)
/// sobre o sender interno (design §5.3, §6.4, Req 8, RNF 3, DD-004).
///
/// Políticas aplicadas em ordem (externo → interno):
/// <list type="number">
///   <item><description>Captura de exceção residual — converte qualquer <see cref="Exception"/> em <see cref="SendResult"/> (Req 3.5).</description></item>
///   <item><description>Circuit breaker — abre após <see cref="ResilientEmailSenderOptions.CircuitBreakerFailureThreshold"/> falhas consecutivas de provedor; exclui <see cref="SendStatus.Bounced"/> e <see cref="SendStatus.Suppressed"/> da contagem (Req 7.3, RNF-3.3).</description></item>
///   <item><description>Retry — até <see cref="ResilientEmailSenderOptions.MaxRetryAttempts"/> retentativas com backoff exponencial + jitter para <see cref="SendStatus.TransientFailure"/> (RNF-3.2).</description></item>
///   <item><description>Timeout por tentativa — <see cref="ResilientEmailSenderOptions.TimeoutPerAttemptSeconds"/> (RNF-3.1).</description></item>
/// </list>
///
/// Posição na cadeia (design §5.3):
/// <code>Caller → ResilientEmailSender → [inner sender / BrandingEmailDecorator → ...]</code>
///
/// <para>Nunca lança exceção ao chamador — toda falha resulta em <see cref="SendResult"/>.</para>
/// <para>Validação de borda (<see cref="EmailMessageValidator"/>) ocorre antes do pipeline de resiliência,
/// sem consumir tentativa nem cota de envio (Req 2.5).</para>
/// </summary>
public sealed class ResilientEmailSender : IEmailSender
{
    // -------------------------------------------------------------------------
    // Código de correlação de fallback para mensagens nulas
    // -------------------------------------------------------------------------
    private const string UnknownCorrelationId = "unknown";
    private const string ProviderName = "resilient";

    // -------------------------------------------------------------------------
    // Dependências
    // -------------------------------------------------------------------------
    private readonly IEmailSender _inner;
    private readonly ResilientEmailSenderOptions _options;
    private readonly ILogger<ResilientEmailSender> _logger;
    private readonly EmailMessageValidator _validator;
    private readonly ResiliencePipeline<SendResult> _pipeline;

    /// <summary>
    /// Constrói o <see cref="ResilientEmailSender"/> e monta o pipeline Polly.
    /// </summary>
    /// <param name="inner">Sender interno (ex.: <c>BrandingEmailDecorator</c> → <c>ProviderEmailSender</c>).</param>
    /// <param name="options">Opções de resiliência injetadas via <c>IOptions&lt;ResilientEmailSenderOptions&gt;</c>.</param>
    /// <param name="logger">Logger estruturado (sem PII — RNF 4).</param>
    public ResilientEmailSender(
        IEmailSender inner,
        IOptions<ResilientEmailSenderOptions> options,
        ILogger<ResilientEmailSender> logger)
    {
        _inner = inner;
        _options = options.Value;
        _logger = logger;
        _validator = new EmailMessageValidator();
        _pipeline = BuildPipeline();
    }

    // -------------------------------------------------------------------------
    // IEmailSender.SendAsync
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<SendResult> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        // Guard: mensagem nula → PermanentFailure imediato sem chamar pipeline
        if (message is null)
        {
            _logger.LogWarning(
                "SendAsync chamado com EmailMessage nula. Retornando PermanentFailure sem chamar o sender interno.");
            return CreatePermanentFailure(
                code: FailureCode.MissingSubjectOrBody,
                message: "EmailMessage não pode ser nula.",
                correlationId: UnknownCorrelationId);
        }

        // Validação de borda antes do pipeline (Req 2.5, design §5.5)
        // Retorna falha permanente sem consumir tentativa ou cota de envio.
        var validationFailure = _validator.Validate(message, provider: ProviderName, attemptCount: 0);
        if (validationFailure is not null)
            return validationFailure;

        // Executar dentro do pipeline de resiliência com captura total de exceções
        try
        {
            return await _pipeline.ExecuteAsync(
                async ct => await _inner.SendAsync(message, ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Captura residual: qualquer exceção não tratada pelo pipeline
            // (ex.: BrokenCircuitException, TimeoutRejectedException quando propagadas)
            return HandleResidualException(ex, message.CorrelationId);
        }
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckAvailabilityAsync(
        CancellationToken cancellationToken = default) =>
        _inner.CheckAvailabilityAsync(cancellationToken);

    // -------------------------------------------------------------------------
    // Construção do pipeline Polly v8
    // -------------------------------------------------------------------------

    /// <summary>
    /// Constrói o <see cref="ResiliencePipeline{T}"/> com timeout + retry + circuit breaker.
    ///
    /// Ordem das estratégias (da mais interna para mais externa, conforme Polly v8):
    /// <list type="number">
    ///   <item><description>Timeout por tentativa (mais interno — envolve cada tentativa individualmente).</description></item>
    ///   <item><description>Retry com backoff exponencial + jitter (envolve timeout).</description></item>
    ///   <item><description>Circuit breaker (mais externo — envolve toda a cadeia retry+timeout).</description></item>
    /// </list>
    ///
    /// Em Polly v8, a ordem de adição ao builder é a ordem de execução de fora para dentro.
    /// </summary>
    private ResiliencePipeline<SendResult> BuildPipeline()
    {
        var builder = new ResiliencePipelineBuilder<SendResult>();

        // 1. Circuit breaker (mais externo)
        // Conta apenas falhas transientes de provedor — Bounced e Suppressed excluídos (Req 7.3)
        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<SendResult>
        {
            // Abre após N falhas consecutivas
            MinimumThroughput = _options.CircuitBreakerFailureThreshold,
            FailureRatio = 1.0,             // qualquer falha conta (threshold = quantidade consecutiva)
            SamplingDuration = TimeSpan.FromSeconds(
                Math.Max(_options.CircuitBreakerBreakDurationSeconds, 1)),
            BreakDuration = TimeSpan.FromSeconds(
                Math.Max(_options.CircuitBreakerBreakDurationSeconds, 1)),
            // Predicate: conta como falha somente TransientFailure ou PermanentFailure
            // Bounced e Suppressed NÃO incrementam o breaker (Req 7.3)
            ShouldHandle = new PredicateBuilder<SendResult>()
                .HandleResult(r =>
                    r.Status == SendStatus.TransientFailure
                    || r.Status == SendStatus.PermanentFailure),
            OnOpened = args =>
            {
                _logger.LogError(
                    "Circuit breaker aberto após falhas consecutivas. " +
                    "Código: {FailureCode}. Break duration: {BreakDuration}s. (NOTIF-ERR-011, Req 8.3)",
                    FailureCode.CircuitBreakerOpen,
                    _options.CircuitBreakerBreakDurationSeconds);
                return ValueTask.CompletedTask;
            },
            OnClosed = args =>
            {
                _logger.LogInformation("Circuit breaker fechado — provedor disponível novamente.");
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = args =>
            {
                _logger.LogInformation("Circuit breaker em half-open — testando disponibilidade do provedor.");
                return ValueTask.CompletedTask;
            }
        });

        // 2. Retry com backoff exponencial + jitter (camada intermediária)
        // Retenta somente TransientFailure (Req 8, RNF-3.2)
        builder.AddRetry(new RetryStrategyOptions<SendResult>
        {
            MaxRetryAttempts = _options.MaxRetryAttempts,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = _options.UseJitter,
            Delay = TimeSpan.FromMilliseconds(Math.Max(_options.BaseRetryDelayMs, 0)),
            // Retenta somente falhas transientes
            ShouldHandle = new PredicateBuilder<SendResult>()
                .HandleResult(r => r.Status == SendStatus.TransientFailure),
            OnRetry = args =>
            {
                _logger.LogWarning(
                    "Tentativa {Attempt} de {MaxAttempts} após falha transiente. " +
                    "Aguardando {Delay}ms. (Req 8, RNF-3.2)",
                    args.AttemptNumber + 1,
                    _options.MaxRetryAttempts + 1,
                    args.RetryDelay.TotalMilliseconds);
                return ValueTask.CompletedTask;
            }
        });

        // 3. Timeout por tentativa (mais interno — envolve cada chamada individual)
        builder.AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(_options.TimeoutPerAttemptSeconds, 1)),
            OnTimeout = args =>
            {
                _logger.LogWarning(
                    "Timeout de {TimeoutSeconds}s excedido na tentativa ao provedor. (NOTIF-ERR-012, RNF-3.1)",
                    _options.TimeoutPerAttemptSeconds);
                return ValueTask.CompletedTask;
            }
        });

        return builder.Build();
    }

    // -------------------------------------------------------------------------
    // Tratamento de exceções residuais
    // -------------------------------------------------------------------------

    /// <summary>
    /// Trata exceções que escapam do pipeline (ex.: <see cref="BrokenCircuitException"/>,
    /// <see cref="TimeoutRejectedException"/>, ou qualquer outra exceção não mapeada).
    ///
    /// Sempre retorna <see cref="SendResult"/> — nunca relança (Req 3.5).
    /// </summary>
    private SendResult HandleResidualException(Exception ex, string correlationId)
    {
        switch (ex)
        {
            case BrokenCircuitException:
                _logger.LogWarning(
                    "Circuit breaker aberto — falha rápida sem chamar o provedor. " +
                    "CorrelationId={CorrelationId}. (NOTIF-ERR-011)",
                    correlationId);
                return CreateTransientFailure(
                    code: FailureCode.CircuitBreakerOpen,
                    message: "Provedor indisponível; circuit breaker aberto.",
                    correlationId: correlationId,
                    isRetriable: true);

            case TimeoutRejectedException:
                _logger.LogWarning(
                    "Timeout residual capturado. CorrelationId={CorrelationId}. (NOTIF-ERR-012)",
                    correlationId);
                return CreateTransientFailure(
                    code: FailureCode.AttemptTimeout,
                    message: "Tempo limite da tentativa excedido.",
                    correlationId: correlationId,
                    isRetriable: true);

            case OperationCanceledException when ex is not TimeoutRejectedException:
                _logger.LogWarning(
                    "Operação cancelada. CorrelationId={CorrelationId}.",
                    correlationId);
                return CreateTransientFailure(
                    code: FailureCode.TransientProviderFailure,
                    message: "Operação cancelada antes de completar.",
                    correlationId: correlationId,
                    isRetriable: true);

            default:
                _logger.LogError(
                    ex,
                    "Exceção não classificada capturada pelo ResilientEmailSender. " +
                    "CorrelationId={CorrelationId}. Tipo={ExceptionType}. (NOTIF-ERR-010)",
                    correlationId,
                    ex.GetType().Name);
                return CreateTransientFailure(
                    code: FailureCode.TransientProviderFailure,
                    message: "Falha transiente do provedor; tentativas esgotadas.",
                    correlationId: correlationId,
                    isRetriable: true);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers de construção de SendResult
    // -------------------------------------------------------------------------

    private static SendResult CreateTransientFailure(
        string code, string message, string correlationId, bool isRetriable) =>
        new(
            status: SendStatus.TransientFailure,
            correlationId: correlationId,
            provider: ProviderName,
            attemptCount: 1,
            messageId: null,
            reason: new FailureReason(code, message, IsRetriable: isRetriable));

    private static SendResult CreatePermanentFailure(
        string code, string message, string correlationId) =>
        new(
            status: SendStatus.PermanentFailure,
            correlationId: correlationId,
            provider: ProviderName,
            attemptCount: 0,
            messageId: null,
            reason: new FailureReason(code, message, IsRetriable: false));
}

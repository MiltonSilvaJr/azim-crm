using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NotificationDelivery.Application.Telemetry;

/// <summary>
/// Métricas do módulo de entrega de notificações via <see cref="System.Diagnostics.Metrics"/>.
///
/// Todas as métricas usam prefixo <c>email_send_</c> em snake_case (TASK-20, RNF 5).
/// Nenhuma métrica ou atributo de span contém PII (RNF 4, DD-008).
///
/// Métricas expostas (7 instruments):
/// <list type="bullet">
///   <item><description><c>email_send_attempts_total</c> — contador de tentativas de envio.</description></item>
///   <item><description><c>email_send_success_total</c> — contador de envios bem-sucedidos.</description></item>
///   <item><description><c>email_send_failure_total</c> — contador de falhas (permanentes + transientes).</description></item>
///   <item><description><c>email_bounce_total</c> — contador de bounces (hard bounce).</description></item>
///   <item><description><c>email_suppressed_total</c> — contador de supressões.</description></item>
///   <item><description><c>email_send_duration_seconds</c> — histograma de latência de envio.</description></item>
///   <item><description><c>email_circuit_breaker_state</c> — gauge do estado do circuit breaker (0=fechado, 1=aberto, 2=half-open).</description></item>
/// </list>
///
/// ActivitySource:
/// <c>NotificationDelivery.EmailSend</c> — gera spans com atributos sem PII.
/// </summary>
public sealed class NotificationDeliveryMetrics : IDisposable
{
    // -------------------------------------------------------------------------
    // Nomes padronizados (snake_case, prefixo email_send_)
    // -------------------------------------------------------------------------

    /// <summary>Nome do <see cref="Meter"/> do módulo.</summary>
    public const string MeterName = "NotificationDelivery";

    /// <summary>Nome do <see cref="ActivitySource"/> do módulo.</summary>
    public const string ActivitySourceName = "NotificationDelivery.EmailSend";

    // -------------------------------------------------------------------------
    // Instruments
    // -------------------------------------------------------------------------

    private readonly Meter _meter;

    /// <summary>Contador de tentativas de envio (por provider e tenant).</summary>
    private readonly Counter<long> _attemptsTotal;

    /// <summary>Contador de envios bem-sucedidos (por provider e tenant).</summary>
    private readonly Counter<long> _successTotal;

    /// <summary>Contador de falhas de envio (por provider, tenant e failure_code).</summary>
    private readonly Counter<long> _failureTotal;

    /// <summary>Contador de bounces (hard bounce) (por provider e tenant).</summary>
    private readonly Counter<long> _bounceTotal;

    /// <summary>Contador de supressões (por provider e tenant).</summary>
    private readonly Counter<long> _suppressedTotal;

    /// <summary>Histograma de latência de envio em segundos (por provider e tenant).</summary>
    private readonly Histogram<double> _durationSeconds;

    /// <summary>
    /// Gauge do estado do circuit breaker.
    /// 0 = fechado (normal), 1 = aberto (falha rápida), 2 = half-open (testando).
    /// </summary>
    private readonly ObservableGauge<int> _circuitBreakerState;

    /// <summary>Estado atual do circuit breaker (lido pelo gauge).</summary>
    private int _circuitBreakerStateValue;

    /// <summary>ActivitySource para geração de spans de envio.</summary>
    public static readonly ActivitySource ActivitySource =
        new(ActivitySourceName, "1.0.0");

    // -------------------------------------------------------------------------
    // Construtor
    // -------------------------------------------------------------------------

    /// <summary>
    /// Inicializa o <see cref="NotificationDeliveryMetrics"/> registrando os 7 instruments.
    /// </summary>
    public NotificationDeliveryMetrics()
    {
        _meter = new Meter(MeterName, "1.0.0");

        _attemptsTotal = _meter.CreateCounter<long>(
            "email_send_attempts_total",
            unit: "{attempt}",
            description: "Número total de tentativas de envio de e-mail.");

        _successTotal = _meter.CreateCounter<long>(
            "email_send_success_total",
            unit: "{message}",
            description: "Número total de e-mails enviados com sucesso.");

        _failureTotal = _meter.CreateCounter<long>(
            "email_send_failure_total",
            unit: "{message}",
            description: "Número total de falhas de envio de e-mail (permanentes e transientes).");

        _bounceTotal = _meter.CreateCounter<long>(
            "email_bounce_total",
            unit: "{message}",
            description: "Número total de bounces de e-mail (hard bounce).");

        _suppressedTotal = _meter.CreateCounter<long>(
            "email_suppressed_total",
            unit: "{message}",
            description: "Número total de e-mails suprimidos pelo provedor.");

        _durationSeconds = _meter.CreateHistogram<double>(
            "email_send_duration_seconds",
            unit: "s",
            description: "Latência de envio de e-mail em segundos (por tentativa do provider).");

        _circuitBreakerState = _meter.CreateObservableGauge<int>(
            "email_circuit_breaker_state",
            observeValue: () => _circuitBreakerStateValue,
            unit: "{state}",
            description: "Estado do circuit breaker: 0=fechado, 1=aberto, 2=half-open.");
    }

    // -------------------------------------------------------------------------
    // Métodos de registro de métricas
    // -------------------------------------------------------------------------

    /// <summary>
    /// Registra uma tentativa de envio.
    /// </summary>
    /// <param name="provider">Nome do provedor (ex.: <c>resend</c>, <c>sendgrid</c>).</param>
    /// <param name="tenantId">ID do tenant (sem PII).</param>
    public void RecordAttempt(string provider, string tenantId) =>
        _attemptsTotal.Add(1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("tenant_id", tenantId));

    /// <summary>
    /// Registra um envio bem-sucedido.
    /// </summary>
    /// <param name="provider">Nome do provedor.</param>
    /// <param name="tenantId">ID do tenant.</param>
    public void RecordSuccess(string provider, string tenantId) =>
        _successTotal.Add(1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("tenant_id", tenantId));

    /// <summary>
    /// Registra uma falha de envio.
    /// </summary>
    /// <param name="provider">Nome do provedor.</param>
    /// <param name="tenantId">ID do tenant.</param>
    /// <param name="failureCode">Código de falha (ex.: <c>NOTIF-ERR-001</c>).</param>
    /// <param name="isTransient">Se a falha é transiente (verdadeiro) ou permanente (falso).</param>
    public void RecordFailure(string provider, string tenantId, string failureCode, bool isTransient) =>
        _failureTotal.Add(1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("tenant_id", tenantId),
            new KeyValuePair<string, object?>("failure_code", failureCode),
            new KeyValuePair<string, object?>("is_transient", isTransient));

    /// <summary>
    /// Registra um bounce (hard bounce).
    /// </summary>
    /// <param name="provider">Nome do provedor.</param>
    /// <param name="tenantId">ID do tenant.</param>
    public void RecordBounce(string provider, string tenantId) =>
        _bounceTotal.Add(1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("tenant_id", tenantId));

    /// <summary>
    /// Registra uma supressão de e-mail.
    /// </summary>
    /// <param name="provider">Nome do provedor.</param>
    /// <param name="tenantId">ID do tenant.</param>
    public void RecordSuppressed(string provider, string tenantId) =>
        _suppressedTotal.Add(1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("tenant_id", tenantId));

    /// <summary>
    /// Registra a duração de uma tentativa de envio.
    /// </summary>
    /// <param name="durationSeconds">Duração em segundos.</param>
    /// <param name="provider">Nome do provedor.</param>
    /// <param name="tenantId">ID do tenant.</param>
    public void RecordDuration(double durationSeconds, string provider, string tenantId) =>
        _durationSeconds.Record(durationSeconds,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("tenant_id", tenantId));

    /// <summary>
    /// Atualiza o estado do circuit breaker.
    /// </summary>
    /// <param name="state">0=fechado, 1=aberto, 2=half-open.</param>
    public void SetCircuitBreakerState(int state) =>
        Interlocked.Exchange(ref _circuitBreakerStateValue, state);

    /// <summary>
    /// Cria um <see cref="Activity"/> para rastrear o envio de e-mail.
    /// Sem PII nos atributos (RNF 4): usa apenas correlationId e tenantId.
    /// </summary>
    /// <param name="correlationId">ID de correlação da mensagem.</param>
    /// <param name="tenantId">ID do tenant.</param>
    /// <param name="provider">Nome do provedor.</param>
    public static Activity? StartSendActivity(string correlationId, string tenantId, string provider)
    {
        var activity = ActivitySource.StartActivity(
            "email.send",
            ActivityKind.Client);

        if (activity is null)
            return null;

        // Atributos sem PII (RNF 4) — correlation_id e tenant_id são seguros
        activity.SetTag("correlation_id", correlationId);
        activity.SetTag("tenant_id", tenantId);
        activity.SetTag("provider", provider);
        activity.SetTag("messaging.system", "email");
        activity.SetTag("messaging.operation", "send");

        return activity;
    }

    /// <inheritdoc/>
    public void Dispose() => _meter.Dispose();
}

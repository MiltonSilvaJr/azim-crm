using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Digest.Infrastructure.Observability;

/// <summary>
/// Instrumentação de métricas e traces do módulo digest (TASK-26, RNF 6.2, RNF 6.4).
/// Métricas em snake_case conforme design §11 e convenção observability.md.
/// Traces via <see cref="ActivitySource"/> (OpenTelemetry-compatível, sem dependência externa).
///
/// Métricas expostas:
/// <list type="bullet">
///   <item><c>digest_jobs_processed_total</c> — contador de jobs processados por tenant.</item>
///   <item><c>digest_emails_sent_total</c> — contador de e-mails enviados com sucesso.</item>
///   <item><c>digest_emails_failed_total</c> — contador de e-mails com falha definitiva.</item>
///   <item><c>digest_processing_duration_seconds</c> — histogram de duração do job (RNF 4.4).</item>
///   <item><c>digest_delivery_rate</c> — gauge de taxa de entrega (KPI-03).</item>
/// </list>
///
/// Sem PII em nenhuma tag/label de métrica ou trace (RNF 3, DD-011).
/// </summary>
public sealed class DigestMetrics : IDisposable
{
    /// <summary>Nome do serviço de instrumentação (source name para OpenTelemetry).</summary>
    public const string ServiceName = "azim.digest";

    // ------------------------------------------------------------------
    // ActivitySource para traces OpenTelemetry (RNF 6.4)
    // ------------------------------------------------------------------

    /// <summary>
    /// Source de atividades para traces distribuídos (OpenTelemetry-compatível).
    /// Cada etapa do job (seleção → composição → envio) cria um span filho.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");

    // ------------------------------------------------------------------
    // Meter e instrumentos de métricas (RNF 6.2, design §11)
    // ------------------------------------------------------------------

    private readonly Meter _meter;

    /// <summary>Contador de jobs de digest processados por tenant.</summary>
    public readonly Counter<long> JobsProcessedTotal;

    /// <summary>Contador de e-mails enviados com sucesso (status = sent).</summary>
    public readonly Counter<long> EmailsSentTotal;

    /// <summary>Contador de e-mails com falha definitiva (status = failed).</summary>
    public readonly Counter<long> EmailsFailedTotal;

    /// <summary>Histogram de duração do processamento de um tenant em segundos (RNF 4.4).</summary>
    public readonly Histogram<double> ProcessingDurationSeconds;

    /// <summary>Gauge de taxa de entrega — calculado como sent / (sent + failed) por período (KPI-03).</summary>
    public readonly ObservableGauge<double> DeliveryRateGauge;

    // Estado interno para o gauge de delivery rate
    private long _sentCount;
    private long _failedCount;

    /// <summary>
    /// Constrói a instrumentação de métricas do módulo digest.
    /// </summary>
    public DigestMetrics()
    {
        _meter = new Meter(ServiceName, "1.0.0");

        JobsProcessedTotal = _meter.CreateCounter<long>(
            name: "digest_jobs_processed_total",
            unit: "{jobs}",
            description: "Número total de jobs de digest processados por tenant.");

        EmailsSentTotal = _meter.CreateCounter<long>(
            name: "digest_emails_sent_total",
            unit: "{emails}",
            description: "Número total de e-mails de digest enviados com sucesso.");

        EmailsFailedTotal = _meter.CreateCounter<long>(
            name: "digest_emails_failed_total",
            unit: "{emails}",
            description: "Número total de e-mails de digest com falha definitiva.");

        ProcessingDurationSeconds = _meter.CreateHistogram<double>(
            name: "digest_processing_duration_seconds",
            unit: "s",
            description: "Duração do processamento de um job de digest por tenant em segundos (RNF 4.4).");

        // Gauge observável: taxa de entrega = sent / (sent + failed); 1.0 quando sem falhas
        DeliveryRateGauge = _meter.CreateObservableGauge(
            name: "digest_delivery_rate",
            observeValue: () => ComputeDeliveryRate(),
            unit: "{ratio}",
            description: "Taxa de entrega do digest: emails_sent / (emails_sent + emails_failed) — KPI-03.");
    }

    // ------------------------------------------------------------------
    // Métodos de conveniência para registrar eventos sem PII
    // ------------------------------------------------------------------

    /// <summary>
    /// Registra que um job de digest foi processado para um tenant.
    /// Tags: tenant_id apenas (UUID opaco — sem PII, DD-011).
    /// </summary>
    public void RecordJobProcessed(Guid tenantId, int recipientCount)
    {
        JobsProcessedTotal.Add(1,
            new KeyValuePair<string, object?>("tenant_id", tenantId.ToString()),
            new KeyValuePair<string, object?>("recipient_count", recipientCount));
    }

    /// <summary>
    /// Registra que um e-mail foi enviado com sucesso.
    /// Tags: tenant_id apenas — sem e-mail do destinatário (RNF 3, DD-011).
    /// </summary>
    public void RecordEmailSent(Guid tenantId)
    {
        Interlocked.Increment(ref _sentCount);
        EmailsSentTotal.Add(1,
            new KeyValuePair<string, object?>("tenant_id", tenantId.ToString()));
    }

    /// <summary>
    /// Registra falha definitiva de envio de e-mail.
    /// Tags: tenant_id apenas — sem e-mail do destinatário (RNF 3, DD-011).
    /// </summary>
    public void RecordEmailFailed(Guid tenantId)
    {
        Interlocked.Increment(ref _failedCount);
        EmailsFailedTotal.Add(1,
            new KeyValuePair<string, object?>("tenant_id", tenantId.ToString()));
    }

    /// <summary>
    /// Registra a duração de processamento de um job de tenant.
    /// </summary>
    public void RecordProcessingDuration(Guid tenantId, TimeSpan duration)
    {
        ProcessingDurationSeconds.Record(
            duration.TotalSeconds,
            new KeyValuePair<string, object?>("tenant_id", tenantId.ToString()));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private double ComputeDeliveryRate()
    {
        var sent = Interlocked.Read(ref _sentCount);
        var failed = Interlocked.Read(ref _failedCount);
        var total = sent + failed;
        return total == 0 ? 1.0 : (double)sent / total;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _meter.Dispose();
        ActivitySource.Dispose();
    }
}

using System.Diagnostics.Metrics;
using Digest.Infrastructure.Observability;
using FluentAssertions;
using Xunit;

namespace Digest.Infrastructure.Tests.Observability;

/// <summary>
/// Testes de observabilidade do módulo digest (TASK-26, RNF 6.2, RNF 6.4, DD-011).
/// Verifica:
/// - Métricas digest_* são registradas corretamente (RNF 6.2).
/// - digest_processing_duration_seconds é registrado ao final de cada job (RNF 4.4).
/// - Nenhuma tag/label de métrica contém PII (e-mail, nome, conteúdo) — DD-011, RNF 3.
/// - digest_delivery_rate é calculado corretamente (KPI-03).
/// </summary>
public sealed class DigestMetricsTests : IDisposable
{
    private readonly DigestMetrics _metrics;
    private readonly MeterListener _meterListener;

    // Captura de valores registrados
    private readonly List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> _recorded = [];
    private readonly object _lock = new();

    public DigestMetricsTests()
    {
        _metrics = new DigestMetrics();

        _meterListener = new MeterListener();
        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == DigestMetrics.ServiceName)
                listener.EnableMeasurementEvents(instrument, null);
        };

        _meterListener.SetMeasurementEventCallback<long>(OnMeasurementLong);
        _meterListener.SetMeasurementEventCallback<double>(OnMeasurementDouble);
        _meterListener.Start();
    }

    public void Dispose()
    {
        _meterListener.Dispose();
        _metrics.Dispose();
    }

    // ---------------------------------------------------------------
    // digest_jobs_processed_total (RNF 6.2)
    // ---------------------------------------------------------------

    [Fact(DisplayName = "TASK-26: digest_jobs_processed_total é incrementado ao processar job")]
    public void JobsProcessedTotal_IsRecordedOnJobProcessed()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        _metrics.RecordJobProcessed(tenantId, recipientCount: 5);

        // Assert
        _meterListener.RecordObservableInstruments();
        var recorded = GetRecorded("digest_jobs_processed_total");
        recorded.Should().NotBeEmpty("digest_jobs_processed_total deve ser registrado");
        recorded[0].Value.Should().Be(1);

        // Garantia anti-PII: tag tenant_id é UUID, não e-mail (DD-011)
        recorded[0].Tags.Should().Contain(t => t.Key == "tenant_id" && t.Value!.ToString() == tenantId.ToString(),
            "tag tenant_id deve ser UUID opaco — nunca e-mail (DD-011)");
        recorded[0].Tags.Should().NotContain(t => t.Key == "email",
            "tag 'email' não deve existir em métricas (RNF 3, DD-011)");
    }

    // ---------------------------------------------------------------
    // digest_emails_sent_total (RNF 6.2)
    // ---------------------------------------------------------------

    [Fact(DisplayName = "TASK-26: digest_emails_sent_total é incrementado sem tag de e-mail (sem PII)")]
    public void EmailsSentTotal_IsRecordedWithoutPii()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        _metrics.RecordEmailSent(tenantId);

        // Assert
        var recorded = GetRecorded("digest_emails_sent_total");
        recorded.Should().NotBeEmpty();
        recorded[0].Value.Should().Be(1);

        // Anti-PII: nenhuma tag com dados de e-mail ou conteúdo
        var tagKeys = recorded[0].Tags.Select(t => t.Key).ToList();
        tagKeys.Should().NotContain("email", "e-mail do destinatário não pode ser tag de métrica (RNF 3)");
        tagKeys.Should().NotContain("content", "conteúdo do digest não pode ser tag de métrica (RNF 3)");
        tagKeys.Should().NotContain("name", "nome do usuário não pode ser tag de métrica (RNF 3)");
    }

    // ---------------------------------------------------------------
    // digest_emails_failed_total (RNF 6.2)
    // ---------------------------------------------------------------

    [Fact(DisplayName = "TASK-26: digest_emails_failed_total é incrementado em falha definitiva")]
    public void EmailsFailedTotal_IsRecordedOnDefinitiveFailure()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        _metrics.RecordEmailFailed(tenantId);

        // Assert
        var recorded = GetRecorded("digest_emails_failed_total");
        recorded.Should().NotBeEmpty();
        recorded[0].Value.Should().Be(1);
    }

    // ---------------------------------------------------------------
    // digest_processing_duration_seconds (RNF 4.4)
    // ---------------------------------------------------------------

    [Fact(DisplayName = "TASK-26: digest_processing_duration_seconds é registrado ao final do job")]
    public void ProcessingDurationSeconds_IsRecordedAfterJob()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var duration = TimeSpan.FromSeconds(3.5);

        // Act
        _metrics.RecordProcessingDuration(tenantId, duration);

        // Assert
        var recorded = GetRecorded("digest_processing_duration_seconds");
        recorded.Should().NotBeEmpty("RNF 4.4 exige métrica de duração por job");
        recorded[0].Value.Should().BeApproximately(3.5, 0.001);
    }

    // ---------------------------------------------------------------
    // digest_delivery_rate — gauge (KPI-03)
    // ---------------------------------------------------------------

    [Fact(DisplayName = "TASK-26: digest_delivery_rate retorna 1.0 sem falhas")]
    public void DeliveryRate_IsOneWhenNoFailures()
    {
        // Arrange: somente envios com sucesso
        var tenantId = Guid.NewGuid();
        _metrics.RecordEmailSent(tenantId);
        _metrics.RecordEmailSent(tenantId);

        // Act — força coleta do gauge observável
        _meterListener.RecordObservableInstruments();

        var recorded = GetRecorded("digest_delivery_rate");
        recorded.Should().NotBeEmpty();
        recorded.Last().Value.Should().BeApproximately(1.0, 0.001,
            "sem falhas, a taxa de entrega deve ser 1.0 (100%)");
    }

    [Fact(DisplayName = "TASK-26: digest_delivery_rate calcula ratio correto com falhas")]
    public void DeliveryRate_CalculatesCorrectRatioWithFailures()
    {
        // Arrange: 8 enviados + 2 falhados = 80% de taxa
        var tenantId = Guid.NewGuid();
        var fresh = new DigestMetrics(); // instância isolada para este teste

        for (int i = 0; i < 8; i++) fresh.RecordEmailSent(tenantId);
        for (int i = 0; i < 2; i++) fresh.RecordEmailFailed(tenantId);

        // Act
        using var listener = new MeterListener();
        double? capturedRate = null;
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "digest_delivery_rate") l.EnableMeasurementEvents(instrument, null);
        };
        listener.SetMeasurementEventCallback<double>((inst, measurement, _, _) =>
        {
            if (inst.Name == "digest_delivery_rate") capturedRate = measurement;
        });
        listener.Start();
        listener.RecordObservableInstruments();

        // Assert
        capturedRate.Should().BeApproximately(0.8, 0.001, "8 enviados de 10 = 80% de taxa de entrega");

        fresh.Dispose();
    }

    // ---------------------------------------------------------------
    // Teste de ausência de PII em telemetria (DD-011, RNF 3.1)
    // ---------------------------------------------------------------

    [Fact(DisplayName = "TASK-26: nenhuma métrica contém e-mail em texto claro (anti-PII)")]
    public void Metrics_DoNotContainEmailInTags()
    {
        // Arrange: simula processamento de um digest com dados de teste (sem e-mail real)
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act: registra todas as métricas disponíveis
        _metrics.RecordJobProcessed(tenantId, 3);
        _metrics.RecordEmailSent(tenantId);
        _metrics.RecordEmailFailed(tenantId);
        _metrics.RecordProcessingDuration(tenantId, TimeSpan.FromSeconds(2));
        _meterListener.RecordObservableInstruments();

        // Assert: nenhuma tag contém padrão de e-mail (@ ou .com) — RNF 3.1, DD-011
        lock (_lock)
        {
            foreach (var (name, value, tags) in _recorded)
            {
                foreach (var tag in tags)
                {
                    var tagValue = tag.Value?.ToString() ?? string.Empty;
                    tagValue.Should().NotContain("@",
                        $"tag '{tag.Key}' na métrica '{name}' não deve conter e-mail (RNF 3.1, DD-011)");
                    tagValue.Should().NotMatchRegex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
                        $"tag '{tag.Key}' na métrica '{name}' não deve conter endereço de e-mail (RNF 3.1)");
                }
            }
        }
    }

    // ---------------------------------------------------------------
    // ActivitySource — verificação de nome do source de traces
    // ---------------------------------------------------------------

    [Fact(DisplayName = "TASK-26: ActivitySource tem nome correto para OpenTelemetry")]
    public void ActivitySource_HasCorrectName()
    {
        DigestMetrics.ActivitySource.Name.Should().Be(DigestMetrics.ServiceName,
            "ActivitySource deve ter nome do serviço para integração OpenTelemetry (RNF 6.4)");
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> GetRecorded(string metricName)
    {
        lock (_lock)
        {
            return _recorded.Where(r => r.Name == metricName).ToList();
        }
    }

    private void OnMeasurementLong(
        Instrument instrument,
        long measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? _)
    {
        lock (_lock)
        {
            _recorded.Add((instrument.Name, (double)measurement, tags.ToArray()));
        }
    }

    private void OnMeasurementDouble(
        Instrument instrument,
        double measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? _)
    {
        lock (_lock)
        {
            _recorded.Add((instrument.Name, measurement, tags.ToArray()));
        }
    }
}

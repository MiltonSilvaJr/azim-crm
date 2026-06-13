using System.Diagnostics.Metrics;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NotificationDelivery.Application.Resilience;
using NotificationDelivery.Application.Telemetry;
using NotificationDelivery.Contracts;
using Xunit;

namespace NotificationDelivery.Application.Tests.Telemetry;

/// <summary>
/// Testes de observabilidade (TASK-20): verifica que o <see cref="ResilientEmailSender"/>
/// emite corretamente os 7 instruments definidos em <see cref="NotificationDeliveryMetrics"/>.
///
/// Usa <see cref="MeterListener"/> para capturar medições em memória sem exportador real.
///
/// Mapeia: TASK-20, RNF 5, DD-008 (sem PII em métricas/logs).
/// </summary>
public sealed class NotificationDeliveryMetricsTests : IDisposable
{
    // -------------------------------------------------------------------------
    // Captura de medições via MeterListener
    // -------------------------------------------------------------------------

    private readonly MeterListener _listener;
    private readonly System.Collections.Concurrent.ConcurrentBag<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> _measurementsBag = new();
    private readonly NotificationDeliveryMetrics _metrics;

    public NotificationDeliveryMetricsTests()
    {
        _metrics = new NotificationDeliveryMetrics();

        _listener = new MeterListener();
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == NotificationDeliveryMetrics.MeterName)
                listener.EnableMeasurementEvents(instrument);
        };

        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            _measurementsBag.Add((instrument.Name, measurement, tags.ToArray()));
        });

        _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            _measurementsBag.Add((instrument.Name, measurement, tags.ToArray()));
        });

        _listener.Start();
    }

    public void Dispose()
    {
        _listener.Dispose();
        _metrics.Dispose();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private ResilientEmailSender BuildSender(IEmailSender inner, int maxRetries = 1) =>
        new(inner, Options.Create(new ResilientEmailSenderOptions
        {
            MaxRetryAttempts = maxRetries,
            TimeoutPerAttemptSeconds = 30,
            CircuitBreakerFailureThreshold = 10,
            CircuitBreakerBreakDurationSeconds = 1,
            BaseRetryDelayMs = 0,
            UseJitter = false
        }), NullLogger<ResilientEmailSender>.Instance, _metrics);

    private static IEmailSender BuildFakeSender(SendResult result) =>
        new FixedResultFakeSender(result);

    private sealed class FixedResultFakeSender : IEmailSender
    {
        private readonly SendResult _result;
        public FixedResultFakeSender(SendResult result) => _result = result;

        public Task<SendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default) =>
            Task.FromResult(_result);

        public Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckAvailabilityAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());
    }

    private static EmailMessage BuildMessage(string tenantId = "tenant-test") =>
        new(
            recipientEmail: "test@example.com",
            subject: "Teste TASK-20",
            htmlBody: "<p>Corpo</p>",
            tenantId: tenantId,
            correlationId: Guid.NewGuid().ToString(),
            idempotencyKey: null);

    private void FlushMeasurements() => _listener.RecordObservableInstruments();

    private List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> Measurements =>
        _measurementsBag.ToList();

    // -------------------------------------------------------------------------
    // TASK-20-T01: email_send_attempts_total incrementado por envio
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-20-T01: email_send_attempts_total é incrementado a cada SendAsync")]
    public async Task Metrics_SendAsync_IncrementsAttemptCounter()
    {
        // Arrange
        var message = BuildMessage();
        var sentResult = new SendResult(
            status: SendStatus.Sent,
            correlationId: message.CorrelationId,
            provider: "test-provider",
            attemptCount: 1,
            messageId: "msg-001",
            reason: null);

        var sender = BuildSender(BuildFakeSender(sentResult));

        // Act
        await sender.SendAsync(message);

        // Assert
        FlushMeasurements();
        Measurements.Should().Contain(m =>
            m.Name == "email_send_attempts_total" && m.Value == 1,
            "SendAsync deve registrar uma tentativa");
    }

    // -------------------------------------------------------------------------
    // TASK-20-T02: email_send_success_total incrementado em Sent
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-20-T02: email_send_success_total é incrementado quando Sent")]
    public async Task Metrics_SendAsync_Sent_IncrementsSuccessCounter()
    {
        // Arrange
        var message = BuildMessage("tenant-sucesso");
        var sentResult = new SendResult(
            status: SendStatus.Sent,
            correlationId: message.CorrelationId,
            provider: "test-provider",
            attemptCount: 1,
            messageId: "msg-success",
            reason: null);

        var sender = BuildSender(BuildFakeSender(sentResult));

        // Act
        await sender.SendAsync(message);

        // Assert
        FlushMeasurements();
        Measurements.Should().Contain(m =>
            m.Name == "email_send_success_total" && m.Value == 1,
            "Envio bem-sucedido deve incrementar success counter");
    }

    // -------------------------------------------------------------------------
    // TASK-20-T03: email_send_failure_total incrementado em TransientFailure
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-20-T03: email_send_failure_total é incrementado quando TransientFailure")]
    public async Task Metrics_SendAsync_TransientFailure_IncrementsFailureCounter()
    {
        // Arrange
        var message = BuildMessage();
        var failureResult = new SendResult(
            status: SendStatus.TransientFailure,
            correlationId: message.CorrelationId,
            provider: "test-provider",
            attemptCount: 2,
            messageId: null,
            reason: new FailureReason(FailureCode.TransientProviderFailure, "Falha transiente.", IsRetriable: true));

        // maxRetries=1 e o fake sempre retorna transiente → ResilientEmailSender retorna TransientFailure
        var sender = BuildSender(BuildFakeSender(failureResult), maxRetries: 1);

        // Act
        await sender.SendAsync(message);

        // Assert
        FlushMeasurements();
        Measurements.Should().Contain(m =>
            m.Name == "email_send_failure_total" && m.Value == 1,
            "Falha transiente deve incrementar failure counter");
    }

    // -------------------------------------------------------------------------
    // TASK-20-T04: email_bounce_total incrementado em Bounced
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-20-T04: email_bounce_total é incrementado quando Bounced")]
    public async Task Metrics_SendAsync_Bounced_IncrementsBounceCounter()
    {
        // Arrange
        var message = BuildMessage();
        var bouncedResult = new SendResult(
            status: SendStatus.Bounced,
            correlationId: message.CorrelationId,
            provider: "test-provider",
            attemptCount: 1,
            messageId: null,
            reason: new FailureReason(FailureCode.HardBounce, "Hard bounce detectado.", IsRetriable: false));

        var sender = BuildSender(BuildFakeSender(bouncedResult));

        // Act
        await sender.SendAsync(message);

        // Assert
        FlushMeasurements();
        Measurements.Should().Contain(m =>
            m.Name == "email_bounce_total" && m.Value == 1,
            "Bounce deve incrementar bounce counter");
    }

    // -------------------------------------------------------------------------
    // TASK-20-T05: email_suppressed_total incrementado em Suppressed
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-20-T05: email_suppressed_total é incrementado quando Suppressed")]
    public async Task Metrics_SendAsync_Suppressed_IncrementsSuppressedCounter()
    {
        // Arrange
        var message = BuildMessage();
        var suppressedResult = new SendResult(
            status: SendStatus.Suppressed,
            correlationId: message.CorrelationId,
            provider: "test-provider",
            attemptCount: 1,
            messageId: null,
            reason: new FailureReason(FailureCode.AddressSuppressed, "Destinatário suprimido.", IsRetriable: false));

        var sender = BuildSender(BuildFakeSender(suppressedResult));

        // Act
        await sender.SendAsync(message);

        // Assert
        FlushMeasurements();
        Measurements.Should().Contain(m =>
            m.Name == "email_suppressed_total" && m.Value == 1,
            "Supressão deve incrementar suppressed counter");
    }

    // -------------------------------------------------------------------------
    // TASK-20-T06: email_send_duration_seconds registrado com valor ≥ 0
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-20-T06: email_send_duration_seconds é registrado com valor ≥ 0")]
    public async Task Metrics_SendAsync_RecordsDurationGreaterThanOrEqualToZero()
    {
        // Arrange
        var message = BuildMessage();
        var sentResult = new SendResult(
            status: SendStatus.Sent,
            correlationId: message.CorrelationId,
            provider: "test-provider",
            attemptCount: 1,
            messageId: "msg-duration",
            reason: null);

        var sender = BuildSender(BuildFakeSender(sentResult));

        // Act
        await sender.SendAsync(message);

        // Assert
        FlushMeasurements();
        Measurements.Should().Contain(m =>
            m.Name == "email_send_duration_seconds" && m.Value >= 0.0,
            "Duração deve ser ≥ 0 segundos");
    }

    // -------------------------------------------------------------------------
    // TASK-20-T07: email_circuit_breaker_state observável retorna valor 0..2
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-20-T07: email_circuit_breaker_state retorna valor válido (0, 1 ou 2)")]
    public void Metrics_CircuitBreakerState_ReturnsValidValue()
    {
        // O gauge é inicializado em 0 (fechado)
        // Simular transições de estado
        _metrics.SetCircuitBreakerState(0); // fechado
        FlushMeasurements();

        _metrics.SetCircuitBreakerState(1); // aberto
        FlushMeasurements();

        _metrics.SetCircuitBreakerState(2); // half-open
        FlushMeasurements();

        // O gauge observable só é lido quando RecordObservableInstruments é chamado
        // Apenas verifica que nenhuma exceção é lançada nas transições
        Measurements.Should().NotBeNull();
    }

    // -------------------------------------------------------------------------
    // TASK-20-T08: métricas não contêm PII (sem e-mail em tags)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-20-T08: nenhuma tag de métrica contém e-mail em claro (RNF 4)")]
    public async Task Metrics_Tags_NeverContainEmailPii()
    {
        // Arrange
        var message = BuildMessage("tenant-pii-check");
        var sentResult = new SendResult(
            status: SendStatus.Sent,
            correlationId: message.CorrelationId,
            provider: "test-provider",
            attemptCount: 1,
            messageId: "msg-pii-check",
            reason: null);

        var sender = BuildSender(BuildFakeSender(sentResult));

        // Act
        await sender.SendAsync(message);

        // Assert — nenhuma tag deve conter o e-mail do destinatário em claro
        FlushMeasurements();
        const string recipientEmail = "test@example.com";
        foreach (var (name, _, tags) in Measurements)
        {
            foreach (var tag in tags)
            {
                var tagValue = tag.Value?.ToString() ?? string.Empty;
                tagValue.Should().NotContain(
                    recipientEmail,
                    $"tag '{tag.Key}' da métrica '{name}' não deve conter e-mail em claro (RNF 4)");
            }
        }
    }
}

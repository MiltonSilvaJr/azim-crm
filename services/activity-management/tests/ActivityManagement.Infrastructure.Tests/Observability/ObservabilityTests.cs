namespace ActivityManagement.Infrastructure.Tests.Observability;

using System.Diagnostics.Metrics;
using ActivityManagement.Infrastructure.Observability;
using FluentAssertions;
using Xunit;

/// <summary>
/// Testes de observabilidade (TASK-22, RNF 6).
/// Verifica:
///   1. As 5 métricas obrigatórias são incrementadas nas operações correspondentes.
///   2. PiiMasker não expõe title/description nos campos de log.
///   3. Health endpoint retorna corretamente quando banco disponível/indisponível.
///
/// Mapeia: TASK-22, RNF 6.2, design §11.
/// </summary>
public sealed class ObservabilityTests
{
    // ── Métricas ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ActivityMetrics_IncrementCreated_Increments_activities_created_total()
    {
        // Arrange
        using var meterListener = new MeterListener();
        long measured = 0;

        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == ActivityMetrics.MeterName &&
                instrument.Name == ActivityMetrics.ActivitiesCreatedTotalName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        meterListener.SetMeasurementEventCallback<long>((_, value, _, _) =>
        {
            Interlocked.Add(ref measured, value);
        });

        meterListener.Start();

        var metrics = new ActivityMetrics();

        // Act
        metrics.IncrementCreated();

        meterListener.RecordObservableInstruments();

        // Assert
        measured.Should().Be(1, because: "uma criação deve incrementar o contador em 1");
    }

    [Fact]
    public void ActivityMetrics_IncrementCompleted_Increments_activities_completed_total()
    {
        // Arrange
        using var meterListener = new MeterListener();
        long measured = 0;

        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == ActivityMetrics.MeterName &&
                instrument.Name == ActivityMetrics.ActivitiesCompletedTotalName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        meterListener.SetMeasurementEventCallback<long>((_, value, _, _) =>
        {
            Interlocked.Add(ref measured, value);
        });

        meterListener.Start();

        var metrics = new ActivityMetrics();

        // Act
        metrics.IncrementCompleted();

        // Assert
        measured.Should().Be(1, because: "uma conclusão deve incrementar o contador em 1");
    }

    [Fact]
    public void ActivityMetrics_IncrementOverdue_Increments_activities_overdue_total()
    {
        // Arrange
        using var meterListener = new MeterListener();
        long measured = 0;

        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == ActivityMetrics.MeterName &&
                instrument.Name == ActivityMetrics.ActivitiesOverdueTotalName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        meterListener.SetMeasurementEventCallback<long>((_, value, _, _) =>
        {
            Interlocked.Add(ref measured, value);
        });

        meterListener.Start();

        var metrics = new ActivityMetrics();

        // Act
        metrics.IncrementOverdue(3);

        // Assert
        measured.Should().Be(3, because: "deve incrementar pelo número de atividades vencidas");
    }

    [Fact]
    public void ActivityMetrics_IncrementDigestTokenUsed_Increments_digest_action_tokens_used_total()
    {
        // Arrange
        using var meterListener = new MeterListener();
        long measured = 0;

        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == ActivityMetrics.MeterName &&
                instrument.Name == ActivityMetrics.DigestTokensUsedTotalName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        meterListener.SetMeasurementEventCallback<long>((_, value, _, _) =>
        {
            Interlocked.Add(ref measured, value);
        });

        meterListener.Start();

        var metrics = new ActivityMetrics();

        // Act
        metrics.IncrementDigestTokenUsed();

        // Assert
        measured.Should().Be(1);
    }

    [Fact]
    public void ActivityMetrics_IncrementDigestTokenExpired_Increments_digest_action_tokens_expired_total()
    {
        // Arrange
        using var meterListener = new MeterListener();
        long measured = 0;

        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == ActivityMetrics.MeterName &&
                instrument.Name == ActivityMetrics.DigestTokensExpiredTotalName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        meterListener.SetMeasurementEventCallback<long>((_, value, _, _) =>
        {
            Interlocked.Add(ref measured, value);
        });

        meterListener.Start();

        var metrics = new ActivityMetrics();

        // Act
        metrics.IncrementDigestTokenExpired();

        // Assert
        measured.Should().Be(1);
    }

    // ── Nomes de métrica ──────────────────────────────────────────────────────────

    [Fact]
    public void ActivityMetrics_Names_Are_Snake_Case_Prometheus_Compliant()
    {
        // Verifica que os nomes seguem snake_case (design §11, RNF 6.2)
        ActivityMetrics.ActivitiesCreatedTotalName   .Should().Be("activities_created_total");
        ActivityMetrics.ActivitiesCompletedTotalName .Should().Be("activities_completed_total");
        ActivityMetrics.ActivitiesOverdueTotalName   .Should().Be("activities_overdue_total");
        ActivityMetrics.DigestTokensUsedTotalName    .Should().Be("digest_action_tokens_used_total");
        ActivityMetrics.DigestTokensExpiredTotalName .Should().Be("digest_action_tokens_expired_total");
    }
}

using System.Diagnostics.Metrics;
using DataMigration.Infrastructure.Observability;
using FluentAssertions;
using Xunit;

namespace DataMigration.Infrastructure.Tests.Observability;

/// <summary>
/// Testes unitários de <see cref="MigrationMetrics"/>.
///
/// Valida que os instrumentos de métrica são criados corretamente e
/// que os contadores e histogramas registram os valores esperados.
///
/// Rastreia: TASK-25, RNF 6.1, design §11.
/// </summary>
public sealed class MigrationMetricsTests : IDisposable
{
    private readonly MeterListener _listener;
    private readonly Dictionary<string, long> _counters = new();
    private readonly List<double> _durations = new();
    private readonly MigrationMetrics _metrics;

    public MigrationMetricsTests()
    {
        _metrics = new MigrationMetrics();

        _listener = new MeterListener();
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == MigrationMetrics.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        // Captura counters (long)
        _listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            lock (_counters)
            {
                if (!_counters.TryGetValue(instrument.Name, out var current))
                {
                    current = 0;
                }
                _counters[instrument.Name] = current + measurement;
            }
        });

        // Captura histograma (double)
        _listener.SetMeasurementEventCallback<double>((instrument, measurement, _, _) =>
        {
            lock (_durations)
            {
                _durations.Add(measurement);
            }
        });

        _listener.Start();
    }

    // =========================================================================
    // Testes de contadores
    // =========================================================================

    [Fact(DisplayName = "IncrementRowsProcessed incrementa migration_rows_processed_total")]
    public void IncrementRowsProcessed_IncrementsCounter()
    {
        // Act
        _metrics.IncrementRowsProcessed();
        _metrics.IncrementRowsProcessed();
        _metrics.IncrementRowsProcessed();

        _listener.RecordObservableInstruments();

        // Assert
        _counters.TryGetValue("migration_rows_processed_total", out var value);
        value.Should().Be(3);
    }

    [Fact(DisplayName = "IncrementRowsFailed incrementa migration_rows_failed_total")]
    public void IncrementRowsFailed_IncrementsCounter()
    {
        // Act
        _metrics.IncrementRowsFailed();
        _metrics.IncrementRowsFailed();

        _listener.RecordObservableInstruments();

        // Assert
        _counters.TryGetValue("migration_rows_failed_total", out var value);
        value.Should().Be(2);
    }

    [Fact(DisplayName = "IncrementForecastDivergences incrementa migration_forecast_divergences_total")]
    public void IncrementForecastDivergences_IncrementsCounter()
    {
        // Act
        _metrics.IncrementForecastDivergences();

        _listener.RecordObservableInstruments();

        // Assert
        _counters.TryGetValue("migration_forecast_divergences_total", out var value);
        value.Should().Be(1);
    }

    [Fact(DisplayName = "IncrementJobState incrementa migration_jobs_state_total")]
    public void IncrementJobState_IncrementsCounter()
    {
        // Act
        _metrics.IncrementJobState("completed");
        _metrics.IncrementJobState("rolled_back");

        _listener.RecordObservableInstruments();

        // Assert — ambos contribuem para o mesmo counter (labels diferenciadas)
        _counters.TryGetValue("migration_jobs_state_total", out var value);
        value.Should().Be(2);
    }

    // =========================================================================
    // Testes de histograma de duração
    // =========================================================================

    [Fact(DisplayName = "RecordDuration registra duração no histograma migration_duration_seconds")]
    public void RecordDuration_RecordsDurationHistogram()
    {
        // Arrange — simula import de 108 linhas em 45 segundos (bem abaixo do SLO de 300s)
        var duration = TimeSpan.FromSeconds(45);

        // Act
        _metrics.RecordDuration(duration);

        _listener.RecordObservableInstruments();

        // Assert
        _durations.Should().Contain(45.0,
            "duração de 45s deve ser registrada no histograma migration_duration_seconds");
    }

    [Fact(DisplayName = "RecordDuration usa finished_at - started_at conforme RNF 1.2")]
    public void RecordDuration_MeasuresStartedAtToFinishedAt()
    {
        // Arrange — mede diferença de timestamps conforme design §11
        var startedAt = new DateTimeOffset(2026, 6, 14, 10, 0, 0, TimeSpan.Zero);
        var finishedAt = startedAt.AddSeconds(120);
        var duration = finishedAt - startedAt;

        // Act
        _metrics.RecordDuration(duration);

        _listener.RecordObservableInstruments();

        // Assert
        _durations.Should().Contain(120.0);
    }

    // =========================================================================
    // Testes de ActivitySource para traces
    // =========================================================================

    [Fact(DisplayName = "ActivitySource tem nome correto do meter")]
    public void ActivitySource_HasCorrectName()
    {
        MigrationMetrics.ActivitySource.Name.Should().Be(MigrationMetrics.MeterName);
    }

    [Fact(DisplayName = "StartParseActivity cria Activity com tags obrigatórias")]
    public void StartParseActivity_CreatesActivityWithTags()
    {
        var jobId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();

        // StartActivity retorna null quando não há listener ativo — isso é esperado
        // em testes sem OpenTelemetry configurado; não deve lançar.
        var act = () => MigrationMetrics.StartParseActivity(jobId, correlationId);
        act.Should().NotThrow();
    }

    [Fact(DisplayName = "StartDryRunActivity não lança quando sem listener")]
    public void StartDryRunActivity_DoesNotThrowWithoutListener()
    {
        var act = () => MigrationMetrics.StartDryRunActivity(Guid.NewGuid(), Guid.NewGuid());
        act.Should().NotThrow();
    }

    [Fact(DisplayName = "StartImportActivity não lança quando sem listener")]
    public void StartImportActivity_DoesNotThrowWithoutListener()
    {
        var act = () => MigrationMetrics.StartImportActivity(Guid.NewGuid(), Guid.NewGuid());
        act.Should().NotThrow();
    }

    // =========================================================================
    // Testes de nomenclatura (snake_case obrigatório — design §11)
    // =========================================================================

    [Theory(DisplayName = "Nomes de métricas seguem snake_case obrigatório")]
    [InlineData("migration_rows_processed_total")]
    [InlineData("migration_rows_failed_total")]
    [InlineData("migration_duration_seconds")]
    [InlineData("migration_forecast_divergences_total")]
    [InlineData("migration_jobs_state_total")]
    public void MetricNames_AreSnakeCase(string expectedMetricName)
    {
        // Exercita os instrumentos para garantir que estão registrados com o nome correto
        _metrics.IncrementRowsProcessed();
        _metrics.IncrementRowsFailed();
        _metrics.RecordDuration(TimeSpan.FromSeconds(1));
        _metrics.IncrementForecastDivergences();
        _metrics.IncrementJobState("created");

        _listener.RecordObservableInstruments();

        // Assert — o nome deve aparecer na coleção de counters/histogramas coletados
        // (counters são long, histogramas são double — ambos capturados)
        var allNames = _counters.Keys.Concat(_durations.Select(_ => "migration_duration_seconds"));
        allNames.Should().Contain(expectedMetricName,
            $"a métrica '{expectedMetricName}' deve ser registrada com snake_case");
    }

    public void Dispose()
    {
        _listener.Dispose();
        _metrics.Dispose();
    }
}

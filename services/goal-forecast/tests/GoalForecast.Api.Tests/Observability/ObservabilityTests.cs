using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using GoalForecast.Api.Tests.Infrastructure;
using GoalForecast.Application.Commands;
using GoalForecast.Application.Common;
using GoalForecast.Application.Ports;
using GoalForecast.Application.Queries;
using GoalForecast.Infrastructure.Metrics;
using NSubstitute;
using AppGoalDto = GoalForecast.Application.Common.GoalDto;

namespace GoalForecast.Api.Tests.Observability;

/// <summary>
/// Testes de observabilidade da Onda 6 — TASK-28.
///
/// Verifica:
/// <list type="bullet">
///   <item>Constantes de métricas existem (snake_case, conforme RNF 7.2).</item>
///   <item>Contadores acessíveis via <see cref="GoalForecastMetrics"/>.</item>
///   <item>Log de escrita contém campos obrigatórios (correlation_id, tenant_id, bu_id, etc.).</item>
///   <item>Métricas de negócio e circuit breaker declaradas com nomes corretos.</item>
/// </list>
///
/// Mapeia: TASK-28, RNF 7, design §11.
/// </summary>
public sealed class ObservabilityTests
{
    // ══════════════════════════════════════════════════════════════════════════
    // Nomes de métricas (snake_case — RNF 7.2)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "Métrica goals_created_total deve existir com nome correto (snake_case)")]
    public void GoalsCreatedTotal_TemNomeCorretoSnakeCase()
    {
        GoalForecastMetrics.GoalsCreatedTotalName
            .Should().Be("goals_created_total");
    }

    [Fact(DisplayName = "Métrica goals_updated_total deve existir com nome correto (snake_case)")]
    public void GoalsUpdatedTotal_TemNomeCorretoSnakeCase()
    {
        GoalForecastMetrics.GoalsUpdatedTotalName
            .Should().Be("goals_updated_total");
    }

    [Fact(DisplayName = "Métrica pipeline_reader_failures_total deve existir com nome correto")]
    public void PipelineReaderFailuresTotal_TemNomeCorretoSnakeCase()
    {
        GoalForecastMetrics.PipelineReaderFailuresTotalName
            .Should().Be("pipeline_reader_failures_total");
    }

    [Fact(DisplayName = "Métrica pipeline_circuit_open_total deve existir com nome correto")]
    public void PipelineCircuitOpenTotal_TemNomeCorretoSnakeCase()
    {
        GoalForecastMetrics.PipelineCircuitOpenTotalName
            .Should().Be("pipeline_circuit_open_total");
    }

    [Fact(DisplayName = "Métrica forecast_panel_requests_total deve existir com nome correto")]
    public void ForecastPanelRequestsTotal_TemNomeCorretoSnakeCase()
    {
        GoalForecastMetrics.ForecastPanelRequestsTotalName
            .Should().Be("forecast_panel_requests_total");
    }

    [Fact(DisplayName = "Métrica forecast_panel_latency_ms deve existir com nome correto")]
    public void ForecastPanelLatencyMs_TemNomeCorretoSnakeCase()
    {
        GoalForecastMetrics.ForecastPanelLatencyMsName
            .Should().Be("forecast_panel_latency_ms");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Nome do meter e ActivitySource
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "MeterName deve ser 'GoalForecast' (identifica o módulo)")]
    public void MeterName_DeveSerGoalForecast()
    {
        GoalForecastMetrics.MeterName
            .Should().Be("GoalForecast");
    }

    [Fact(DisplayName = "ActivitySourceName deve ser 'GoalForecast' (identifica o módulo para traces)")]
    public void ActivitySourceName_DeveSerGoalForecast()
    {
        GoalForecastMetrics.ActivitySourceName
            .Should().Be("GoalForecast");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Instância de GoalForecastMetrics
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "GoalForecastMetrics pode ser instanciado com MeterFactory sem exceção")]
    public void GoalForecastMetrics_PodeSerInstanciadoSemExcecao()
    {
        using var meterFactory = new TestMeterFactory();
        var act = () => new GoalForecastMetrics(meterFactory);
        act.Should().NotThrow();
    }

    [Fact(DisplayName = "GoalForecastMetrics.RecordGoalCreated não lança exceção")]
    public void RecordGoalCreated_NaoLancaExcecao()
    {
        using var meterFactory = new TestMeterFactory();
        var metrics = new GoalForecastMetrics(meterFactory);

        var act = () => metrics.RecordGoalCreated(tenantId: "tenant-1", buId: "bu-1");
        act.Should().NotThrow();
    }

    [Fact(DisplayName = "GoalForecastMetrics.RecordGoalUpdated não lança exceção")]
    public void RecordGoalUpdated_NaoLancaExcecao()
    {
        using var meterFactory = new TestMeterFactory();
        var metrics = new GoalForecastMetrics(meterFactory);

        var act = () => metrics.RecordGoalUpdated(tenantId: "tenant-1", buId: "bu-1");
        act.Should().NotThrow();
    }

    [Fact(DisplayName = "GoalForecastMetrics.RecordPipelineReaderFailure não lança exceção")]
    public void RecordPipelineReaderFailure_NaoLancaExcecao()
    {
        using var meterFactory = new TestMeterFactory();
        var metrics = new GoalForecastMetrics(meterFactory);

        var act = () => metrics.RecordPipelineReaderFailure(tenantId: "tenant-1", buId: "bu-1");
        act.Should().NotThrow();
    }

    [Fact(DisplayName = "GoalForecastMetrics.RecordCircuitOpen não lança exceção")]
    public void RecordCircuitOpen_NaoLancaExcecao()
    {
        using var meterFactory = new TestMeterFactory();
        var metrics = new GoalForecastMetrics(meterFactory);

        var act = () => metrics.RecordCircuitOpen();
        act.Should().NotThrow();
    }

    [Fact(DisplayName = "GoalForecastMetrics.RecordForecastPanelRequest com latência não lança exceção")]
    public void RecordForecastPanelRequest_NaoLancaExcecao()
    {
        using var meterFactory = new TestMeterFactory();
        var metrics = new GoalForecastMetrics(meterFactory);

        var act = () => metrics.RecordForecastPanelRequest(latencyMs: 42.5, pipelineUnavailable: false);
        act.Should().NotThrow();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Campos obrigatórios nos logs (RNF 7.1)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "LoggingBehavior declara campo correlation_id como constante")]
    public void LoggingBehavior_TemCampoCorrelationId()
    {
        // Os campos são constantes declaradas na classe LoggingBehavior
        GoalForecast.Application.Behaviors.LoggingBehavior<object, object>.FieldCorrelationId
            .Should().Be("correlation_id");
    }

    [Fact(DisplayName = "LoggingBehavior declara campo tenant_id como constante")]
    public void LoggingBehavior_TemCampoTenantId()
    {
        GoalForecast.Application.Behaviors.LoggingBehavior<object, object>.FieldTenantId
            .Should().Be("tenant_id");
    }

    [Fact(DisplayName = "LoggingBehavior declara campo bu_id como constante")]
    public void LoggingBehavior_TemCampoBuId()
    {
        GoalForecast.Application.Behaviors.LoggingBehavior<object, object>.FieldBuId
            .Should().Be("bu_id");
    }

    [Fact(DisplayName = "LoggingBehavior não loga valorMeta como campo sensível (RNF-7.3)")]
    public void LoggingBehavior_NaoPossuiCampoValorMeta()
    {
        // Verifica que não há constante FieldValorMeta — o campo nunca deve ser logado
        var type = typeof(GoalForecast.Application.Behaviors.LoggingBehavior<object, object>);
        var constants = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => f.GetRawConstantValue()?.ToString())
            .ToList();

        constants.Should().NotContain("valor_meta",
            because: "valorMeta não deve ser logado como campo de diagnóstico (RNF-7.3)");
        constants.Should().NotContain("valorMeta",
            because: "valorMeta não deve ser logado como campo de diagnóstico (RNF-7.3)");
    }
}

/// <summary>
/// Implementação mínima de IMeterFactory para testes unitários.
/// Cria um Meter real em memória sem telemetria externa.
/// </summary>
internal sealed class TestMeterFactory : IMeterFactory
{
    private readonly List<Meter> _meters = new();

    public Meter Create(MeterOptions options)
    {
        var meter = new Meter(options.Name, options.Version);
        _meters.Add(meter);
        return meter;
    }

    public void Dispose()
    {
        foreach (var meter in _meters)
            meter.Dispose();
        _meters.Clear();
    }
}

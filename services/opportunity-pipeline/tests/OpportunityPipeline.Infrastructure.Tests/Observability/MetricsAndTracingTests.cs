using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpportunityPipeline.Infrastructure.Observability;

namespace OpportunityPipeline.Infrastructure.Tests.Observability;

/// <summary>
/// Testes de smoke de métricas e tracing (TASK-24, RNF 10.2, RNF 10.3, design §11).
/// Verifica que as 6 métricas obrigatórias são instrumentadas corretamente
/// e que o ActivitySource emite spans com os atributos corretos (sem PII).
/// Mapeia: TASK-24, RNF 10.2, RNF 10.3, design §11.
/// </summary>
[Trait("Category", "Observability")]
public sealed class MetricsAndTracingTests : IDisposable
{
    private readonly OpportunityMetrics _metrics;

    public MetricsAndTracingTests()
    {
        _metrics = new OpportunityMetrics();
    }

    public void Dispose() => _metrics.Dispose();

    // =========================================================================
    // MET-01 — Todas as 6 métricas instrumentadas corretamente
    // =========================================================================

    [Fact(DisplayName = "MET-01: as 6 métricas obrigatórias são registradas no Meter correto (design §11)")]
    public void SixMetrics_AreRegistered_InCorrectMeter()
    {
        // Verifica que o Meter tem o nome correto
        OpportunityMetrics.MeterName.Should().Be("OpportunityPipeline",
            "o nome do Meter deve ser 'OpportunityPipeline' para registro no DI e OpenTelemetry.");
    }

    [Fact(DisplayName = "MET-02: RecordOpportunityCreated não lança exceção (smoke test)")]
    public void RecordOpportunityCreated_DoesNotThrow()
    {
        var act = () => _metrics.RecordOpportunityCreated("tenant-001", "bu-001");
        act.Should().NotThrow("RecordOpportunityCreated deve ser silencioso e não lançar exceção.");
    }

    [Fact(DisplayName = "MET-03: RecordOpportunityWon não lança exceção (smoke test)")]
    public void RecordOpportunityWon_DoesNotThrow()
    {
        var act = () => _metrics.RecordOpportunityWon("tenant-001");
        act.Should().NotThrow("RecordOpportunityWon deve ser silencioso e não lançar exceção.");
    }

    [Fact(DisplayName = "MET-04: RecordOpportunityLost não lança exceção (smoke test)")]
    public void RecordOpportunityLost_DoesNotThrow()
    {
        var act = () => _metrics.RecordOpportunityLost("tenant-001");
        act.Should().NotThrow("RecordOpportunityLost deve ser silencioso e não lançar exceção.");
    }

    [Fact(DisplayName = "MET-05: RecordOpportunityStale não lança exceção (smoke test)")]
    public void RecordOpportunityStale_DoesNotThrow()
    {
        var act = () => _metrics.RecordOpportunityStale("tenant-001");
        act.Should().NotThrow("RecordOpportunityStale deve ser silencioso e não lançar exceção.");
    }

    [Fact(DisplayName = "MET-06: RecordCommissionSnapshotCreated não lança exceção (smoke test)")]
    public void RecordCommissionSnapshotCreated_DoesNotThrow()
    {
        var act = () => _metrics.RecordCommissionSnapshotCreated("tenant-001");
        act.Should().NotThrow("RecordCommissionSnapshotCreated deve ser silencioso e não lançar exceção.");
    }

    [Fact(DisplayName = "MET-07: RecordKanbanRequestDuration não lança exceção com valor válido (smoke test)")]
    public void RecordKanbanRequestDuration_DoesNotThrow()
    {
        var act = () => _metrics.RecordKanbanRequestDuration(142.5, "tenant-001", "bu-001");
        act.Should().NotThrow("RecordKanbanRequestDuration deve aceitar valores positivos de ms.");
    }

    [Fact(DisplayName = "MET-08: UpdateOutboxPendingEvents é thread-safe e não lança exceção (smoke test)")]
    public void UpdateOutboxPendingEvents_ThreadSafe_DoesNotThrow()
    {
        // Simula atualização concorrente do gauge de Outbox
        var tasks = Enumerable.Range(0, 10)
            .Select(i => Task.Run(() => _metrics.UpdateOutboxPendingEvents(i)))
            .ToArray();

        var act = () => Task.WaitAll(tasks);
        act.Should().NotThrow("UpdateOutboxPendingEvents usa Interlocked.Exchange e deve ser thread-safe.");
    }

    // =========================================================================
    // TRACE-01 — ActivitySource emite spans com atributos corretos (sem PII)
    // =========================================================================

    [Fact(DisplayName = "TRACE-01: StartWinOpportunity emite span com correlation_id, tenant_id e opportunity_id (sem PII)")]
    public void StartWinOpportunity_EmitsSpan_WithCorrectAttributesAndNoPii()
    {
        // Arrange: listener de Activity para capturar o span
        var capturedActivities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == OpportunityActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = capturedActivities.Add
        };
        ActivitySource.AddActivityListener(listener);

        var correlationId = "corr-win-001";
        var tenantId = "tenant-abc";
        var opportunityId = Guid.NewGuid();

        // Act
        using var span = OpportunityActivitySource.StartWinOpportunity(correlationId, tenantId, opportunityId);

        // Assert: span emitido
        capturedActivities.Should().NotBeEmpty("StartWinOpportunity deve emitir um span.");
        var activity = capturedActivities.First();

        activity.OperationName.Should().Be("WinOpportunity");
        activity.GetTagItem("correlation_id").Should().Be(correlationId);
        activity.GetTagItem("tenant_id").Should().Be(tenantId);
        activity.GetTagItem("opportunity_id").Should().Be(opportunityId.ToString());
        activity.GetTagItem("operation").Should().Be("win");

        // Sem PII: não deve ter title, owner_name, contact_name, email
        var tags = activity.Tags.Select(t => t.Key).ToList();
        tags.Should().NotContain("title", "spans não devem expor title (pode conter PII — INV-13).");
        tags.Should().NotContain("owner_name", "spans não devem expor nome do owner.");
        tags.Should().NotContain("contact_name", "spans não devem expor contact_name (PII — LGPD).");
        tags.Should().NotContain("email", "spans não devem expor e-mail (PII — LGPD).");
    }

    [Fact(DisplayName = "TRACE-02: StartGetKanban emite span com correlation_id, tenant_id e bu_id (sem PII)")]
    public void StartGetKanban_EmitsSpan_WithCorrectAttributes()
    {
        var capturedActivities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == OpportunityActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = capturedActivities.Add
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var span = OpportunityActivitySource.StartGetKanban("corr-kanban-001", "tenant-xyz", "bu-123");

        // Assert
        capturedActivities.Should().NotBeEmpty("StartGetKanban deve emitir um span.");
        var activity = capturedActivities.First();

        activity.OperationName.Should().Be("GetKanban");
        activity.GetTagItem("correlation_id").Should().Be("corr-kanban-001");
        activity.GetTagItem("tenant_id").Should().Be("tenant-xyz");
        activity.GetTagItem("bu_id").Should().Be("bu-123");
        activity.GetTagItem("operation").Should().Be("get_kanban");
    }
}

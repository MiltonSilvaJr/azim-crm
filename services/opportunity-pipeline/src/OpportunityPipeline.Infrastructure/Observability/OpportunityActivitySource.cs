using System.Diagnostics;

namespace OpportunityPipeline.Infrastructure.Observability;

/// <summary>
/// ActivitySource para tracing distribuído do módulo opportunity-pipeline (RNF 10.3, TASK-24).
/// Implementa OpenTelemetry via System.Diagnostics.ActivitySource (.NET padrão).
///
/// Spans principais (design §11):
/// - "WinOpportunity": cobre Win + Freeze + persistência + Outbox (caso de uso mais sensível).
/// - "CreateOpportunity", "MoveStage", "LoseOpportunity", "ReopenOpportunity": operações de escrita.
/// - "GetKanban": rastreamento de consulta Kanban com propagação de correlation_id.
///
/// Todos os spans propagam correlation_id e tenant_id sem PII (RNF 10.3, RNF 10.4).
/// Mapeia: RNF 10.3, design §11, TASK-24.
/// </summary>
public static class OpportunityActivitySource
{
    /// <summary>Nome do ActivitySource — registrado no DI para configuração do OpenTelemetry.</summary>
    public const string SourceName = "OpportunityPipeline";

    private static readonly ActivitySource Source = new(SourceName, version: "1.0.0");

    // =========================================================================
    // Spans de Commands (operações de escrita)
    // =========================================================================

    /// <summary>
    /// Inicia span para WinOpportunity — o mais sensível (Req 14, design §5.1, §16.4).
    /// Cobre: Win() + Freeze snapshot + persistência + Outbox + auditoria em transação única.
    /// Propaga correlation_id e tenant_id sem PII (RNF 10.3, RNF 10.4).
    /// </summary>
    public static Activity? StartWinOpportunity(
        string correlationId,
        string tenantId,
        Guid opportunityId)
    {
        var activity = Source.StartActivity(
            "WinOpportunity",
            ActivityKind.Internal);

        if (activity is null) return null;

        activity.SetTag("correlation_id", correlationId);
        activity.SetTag("tenant_id", tenantId);
        activity.SetTag("opportunity_id", opportunityId.ToString());
        activity.SetTag("operation", "win");
        // Sem PII: sem title, owner_name, contact_name, email, etc.

        return activity;
    }

    /// <summary>Inicia span para CreateOpportunity.</summary>
    public static Activity? StartCreateOpportunity(string correlationId, string tenantId) =>
        StartSpan("CreateOpportunity", correlationId, tenantId, "create");

    /// <summary>Inicia span para MoveStage.</summary>
    public static Activity? StartMoveStage(string correlationId, string tenantId, Guid opportunityId) =>
        StartSpanWithOpportunityId("MoveStage", correlationId, tenantId, opportunityId, "move_stage");

    /// <summary>Inicia span para LoseOpportunity.</summary>
    public static Activity? StartLoseOpportunity(string correlationId, string tenantId, Guid opportunityId) =>
        StartSpanWithOpportunityId("LoseOpportunity", correlationId, tenantId, opportunityId, "lose");

    /// <summary>Inicia span para ReopenOpportunity.</summary>
    public static Activity? StartReopenOpportunity(string correlationId, string tenantId, Guid opportunityId) =>
        StartSpanWithOpportunityId("ReopenOpportunity", correlationId, tenantId, opportunityId, "reopen");

    // =========================================================================
    // Spans de Queries
    // =========================================================================

    /// <summary>
    /// Inicia span para GetKanban — rastreamento de performance p95 (RNF 1, RNF 10.3).
    /// </summary>
    public static Activity? StartGetKanban(string correlationId, string tenantId, string buId) =>
        Source.StartActivity("GetKanban", ActivityKind.Internal)
              ?.AddCorrelation(correlationId)
              .AddTenant(tenantId)
              .SetTag("bu_id", buId)
              .SetTag("operation", "get_kanban");

    // =========================================================================
    // Helpers internos
    // =========================================================================

    private static Activity? StartSpan(
        string name,
        string correlationId,
        string tenantId,
        string operation)
    {
        var activity = Source.StartActivity(name, ActivityKind.Internal);
        if (activity is null) return null;
        activity.SetTag("correlation_id", correlationId);
        activity.SetTag("tenant_id", tenantId);
        activity.SetTag("operation", operation);
        return activity;
    }

    private static Activity? StartSpanWithOpportunityId(
        string name,
        string correlationId,
        string tenantId,
        Guid opportunityId,
        string operation)
    {
        var activity = Source.StartActivity(name, ActivityKind.Internal);
        if (activity is null) return null;
        activity.SetTag("correlation_id", correlationId);
        activity.SetTag("tenant_id", tenantId);
        activity.SetTag("opportunity_id", opportunityId.ToString());
        activity.SetTag("operation", operation);
        return activity;
    }
}

/// <summary>Extensões fluentes para Activity — reduz repetição nos spans.</summary>
internal static class ActivityExtensions
{
    internal static Activity AddCorrelation(this Activity activity, string correlationId)
    {
        activity.SetTag("correlation_id", correlationId);
        return activity;
    }

    internal static Activity AddTenant(this Activity activity, string tenantId)
    {
        activity.SetTag("tenant_id", tenantId);
        return activity;
    }
}

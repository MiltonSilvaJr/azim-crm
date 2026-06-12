namespace AuditLog.Application.Abstractions;

/// <summary>
/// Abstração para os contadores de observabilidade do módulo de auditoria (design §11.2).
/// A implementação concreta usa OpenTelemetry/GCP Cloud Monitoring (ADR-0007).
/// </summary>
public interface IAuditMetrics
{
    /// <summary>
    /// Incrementa o contador <c>audit_events_received_total</c> (RNF-004.1).
    /// Deve ser chamado ao receber um <c>RecordAuditEntryCommand</c>.
    /// </summary>
    void IncrementEventsReceived();

    /// <summary>
    /// Incrementa o contador <c>audit_insert_failures_total</c> (RNF-004.1).
    /// Deve ser chamado quando o INSERT em <c>audit_logs</c> falha.
    /// </summary>
    void IncrementInsertFailures();

    /// <summary>
    /// Incrementa o contador <c>audit_query_without_tenant_context_total</c> (DD-007).
    /// Deve ser chamado quando uma consulta chega sem <c>tenant_id</c> no contexto.
    /// </summary>
    void IncrementQueryWithoutTenantContext();
}

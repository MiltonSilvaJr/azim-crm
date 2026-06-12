namespace AuditLog.Application.Abstractions;

/// <summary>
/// Abstração para as métricas de observabilidade do módulo de auditoria (design §11.2).
/// A implementação concreta usa <c>System.Diagnostics.Metrics</c> com OpenTelemetry (ADR-0007).
/// <para>Nenhuma label ou valor de métrica deve conter PII (RNF-002).</para>
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

    /// <summary>
    /// Registra a latência de um INSERT em <c>audit_logs</c> no histograma
    /// <c>audit_insert_latency_seconds</c> (RNF-003, design §11.2).
    /// </summary>
    /// <param name="seconds">Duração em segundos do INSERT.</param>
    void RecordInsertLatency(double seconds);

    /// <summary>
    /// Incrementa o contador <c>audit_pii_masking_applied_total</c> (REQ-004, design §11.2).
    /// Deve ser chamado quando o <c>PiiMasker</c> aplica mascaramento em pelo menos um campo.
    /// </summary>
    void IncrementPiiMaskingApplied();
}

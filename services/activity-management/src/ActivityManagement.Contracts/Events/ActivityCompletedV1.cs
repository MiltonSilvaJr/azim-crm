namespace ActivityManagement.Contracts.Events;

/// <summary>
/// Contrato de integração do evento <c>activity.completed.v1</c> publicado no tópico
/// <c>azim-activities</c> via Outbox transacional (Req 14.2, design §9).
///
/// Não contém <c>title</c> nem <c>description</c> (PII potencial — RNF 7.2, DD-009).
/// Emitido exatamente uma vez por conclusão efetiva (Req 14.2/14.4, DD-004, PBT-02).
/// Consumidores: digest, audit-log, reporting, opportunity-pipeline.
/// Mapeia: design §9, TASK-20.
/// </summary>
public sealed record ActivityCompletedV1
{
    // ── Envelope canônico ───────────────────────────────────────────────────

    /// <summary>Identificador único do evento (UUID v4). Consumidores deduplicam por este campo.</summary>
    public required Guid EventId { get; init; }

    /// <summary>Tipo do evento (nome estável para roteamento).</summary>
    public string EventType { get; init; } = "activity.completed.v1";

    /// <summary>Versão do contrato de evento.</summary>
    public string EventVersion { get; init; } = "v1";

    /// <summary>Tenant ao qual o evento pertence (isolamento — RNF 1).</summary>
    public required Guid TenantId { get; init; }

    /// <summary>Identificador de correlação da operação que gerou o evento (RNF 6.1).</summary>
    public required Guid CorrelationId { get; init; }

    /// <summary>Identificador da causa (comando ou evento anterior); opcional.</summary>
    public Guid? CausationId { get; init; }

    /// <summary>Instante em que o evento ocorreu (UTC).</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    // ── Payload específico ──────────────────────────────────────────────────

    /// <summary>Identificador da atividade concluída.</summary>
    public required Guid ActivityId { get; init; }

    /// <summary>Usuário responsável pela atividade.</summary>
    public required Guid OwnerId { get; init; }

    /// <summary>Instante de conclusão efetiva da atividade.</summary>
    public required DateTimeOffset CompletedAt { get; init; }

    /// <summary>Oportunidade vinculada (opcional).</summary>
    public Guid? OpportunityId { get; init; }
}

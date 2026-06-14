namespace ActivityManagement.Contracts.Events;

/// <summary>
/// Contrato de integração do evento <c>activity.overdue.v1</c> publicado no tópico
/// <c>azim-activities</c> via Outbox transacional (Req 14.3, design §9).
///
/// Não contém <c>title</c> nem <c>description</c> (PII potencial — RNF 7.2, DD-009).
/// Produzido pelo <c>ScanOverdueActivitiesCommand</c>; deduplicado por <c>(activityId, scanDate)</c>
/// para evitar duplicatas no mesmo ciclo de scan (DD-005, Req 14.3).
/// Consumidor principal: digest.
/// Mapeia: design §9, DD-005, TASK-20.
/// </summary>
public sealed record ActivityOverdueV1
{
    // ── Envelope canônico ───────────────────────────────────────────────────

    /// <summary>Identificador único do evento (UUID v4). Consumidores deduplicam por este campo.</summary>
    public required Guid EventId { get; init; }

    /// <summary>Tipo do evento (nome estável para roteamento).</summary>
    public string EventType { get; init; } = "activity.overdue.v1";

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

    /// <summary>Identificador da atividade vencida.</summary>
    public required Guid ActivityId { get; init; }

    /// <summary>Usuário responsável pela atividade.</summary>
    public required Guid OwnerId { get; init; }

    /// <summary>Instante de vencimento original da atividade.</summary>
    public required DateTimeOffset DueAt { get; init; }

    /// <summary>
    /// Data do ciclo de scan que detectou o vencimento (YYYY-MM-DD em UTC).
    /// Parte da chave de deduplicação <c>(activityId, scanDate)</c> (DD-005).
    /// </summary>
    public required DateOnly ScanDate { get; init; }

    /// <summary>Oportunidade vinculada (opcional).</summary>
    public Guid? OpportunityId { get; init; }
}

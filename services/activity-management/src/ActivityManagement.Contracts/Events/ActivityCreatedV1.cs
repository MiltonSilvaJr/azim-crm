namespace ActivityManagement.Contracts.Events;

/// <summary>
/// Contrato de integração do evento <c>activity.created.v1</c> publicado no tópico
/// <c>azim-activities</c> via Outbox transacional (Req 14.1, design §9).
///
/// Não contém <c>title</c> nem <c>description</c> (PII potencial — RNF 7.2, DD-009).
/// Envelope canônico: <see cref="EventId"/>, <see cref="EventType"/>, <see cref="EventVersion"/>,
/// <see cref="TenantId"/>, <see cref="CorrelationId"/>, <see cref="CausationId"/>, <see cref="OccurredAt"/>.
/// Versionamento: mudança incompatível cria <c>activity.created.v2</c> (retrocompatibilidade — Req 14).
/// Mapeia: design §9, TASK-20.
/// </summary>
public sealed record ActivityCreatedV1
{
    // ── Envelope canônico ───────────────────────────────────────────────────

    /// <summary>Identificador único do evento (UUID v4). Consumidores deduplicam por este campo.</summary>
    public required Guid EventId { get; init; }

    /// <summary>Tipo do evento (nome estável para roteamento).</summary>
    public string EventType { get; init; } = "activity.created.v1";

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

    /// <summary>Identificador da atividade criada.</summary>
    public required Guid ActivityId { get; init; }

    /// <summary>Business Unit ao qual a atividade pertence.</summary>
    public required Guid BuId { get; init; }

    /// <summary>Usuário responsável pela atividade.</summary>
    public required Guid OwnerId { get; init; }

    /// <summary>Tipo da atividade (meeting|follow_up|call|email|task).</summary>
    public required string Type { get; init; }

    /// <summary>Instante de vencimento da atividade.</summary>
    public required DateTimeOffset DueAt { get; init; }

    /// <summary>Oportunidade vinculada (opcional).</summary>
    public Guid? OpportunityId { get; init; }

    /// <summary>Conta vinculada (opcional).</summary>
    public Guid? AccountId { get; init; }
}

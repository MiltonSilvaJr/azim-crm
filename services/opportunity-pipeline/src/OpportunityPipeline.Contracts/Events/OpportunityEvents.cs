using System.Text.Json.Serialization;

namespace OpportunityPipeline.Contracts.Events;

// ============================================================================
// Envelopes de eventos públicos .v1 — sem PII de contato (Req 20.3, RNF 10.4)
// Publicados via Outbox → Cloud Pub/Sub, tópico "azim-opportunities".
// Imutáveis (record). Versionados como .v1.
// Evolução aditiva mantém .v1; quebra de contrato cria .v2.
// Mapeia: design §9, Req 20, TASK-19.
// ============================================================================

/// <summary>
/// Envelope base com metadados de rastreabilidade e versionamento.
/// Segue padrão TRD §9.5.
/// </summary>
public abstract record DomainEventEnvelope
{
    /// <summary>ID único do evento (para idempotência do consumidor).</summary>
    [JsonPropertyName("event_id")]
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>Tipo do evento versionado (ex.: "opportunity.created.v1").</summary>
    [JsonPropertyName("event_type")]
    public abstract string EventType { get; }

    /// <summary>ID do tenant — obrigatório para isolamento multi-tenant.</summary>
    [JsonPropertyName("tenant_id")]
    public required Guid TenantId { get; init; }

    /// <summary>ID do agregado raiz (opportunity_id).</summary>
    [JsonPropertyName("aggregate_id")]
    public required Guid AggregateId { get; init; }

    /// <summary>Tipo do agregado: "Opportunity".</summary>
    [JsonPropertyName("aggregate_type")]
    public string AggregateType => "Opportunity";

    /// <summary>Timestamp UTC do evento.</summary>
    [JsonPropertyName("occurred_at")]
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>Correlation ID propagado da request original (RNF 10).</summary>
    [JsonPropertyName("correlation_id")]
    public string? CorrelationId { get; init; }

    /// <summary>Causation ID (ID do evento que causou este, quando aplicável).</summary>
    [JsonPropertyName("causation_id")]
    public string? CausationId { get; init; }

    /// <summary>Versão do envelope: "v1".</summary>
    [JsonPropertyName("version")]
    public string Version => "v1";
}

/// <summary>
/// Evento público: oportunidade criada.
/// Consumidores: audit-log, reporting, workflow-automation.
/// Sem PII de contato (Req 20.3).
/// Mapeia: design §9, Req 1, Req 20, TASK-19.
/// </summary>
public sealed record OpportunityCreatedV1 : DomainEventEnvelope
{
    public override string EventType => "opportunity.created.v1";

    /// <summary>Número humano imutável (ex.: "AZ-0001").</summary>
    [JsonPropertyName("opportunity_number")]
    public required string OpportunityNumber { get; init; }

    /// <summary>ID da BU.</summary>
    [JsonPropertyName("bu_id")]
    public required Guid BuId { get; init; }

    /// <summary>ID da conta.</summary>
    [JsonPropertyName("account_id")]
    public required Guid AccountId { get; init; }

    /// <summary>ID do proprietário.</summary>
    [JsonPropertyName("owner_id")]
    public required Guid OwnerId { get; init; }

    /// <summary>ID do estágio inicial.</summary>
    [JsonPropertyName("stage_id")]
    public required Guid StageId { get; init; }

    /// <summary>ID do canal de origem.</summary>
    [JsonPropertyName("origin_channel_id")]
    public required Guid OriginChannelId { get; init; }

    /// <summary>ID do ator que criou a oportunidade.</summary>
    [JsonPropertyName("actor_id")]
    public required Guid ActorId { get; init; }
}

/// <summary>
/// Evento público: estágio da oportunidade alterado.
/// Consumidores: audit-log, digest, reporting, workflow-automation.
/// Sem PII de contato.
/// Mapeia: design §9, Req 5, Req 20, TASK-19.
/// </summary>
public sealed record OpportunityStageChangedV1 : DomainEventEnvelope
{
    public override string EventType => "opportunity.stage_changed.v1";

    [JsonPropertyName("from_stage_id")]
    public Guid? FromStageId { get; init; }

    [JsonPropertyName("to_stage_id")]
    public required Guid ToStageId { get; init; }

    [JsonPropertyName("from_category")]
    public string? FromCategory { get; init; }

    [JsonPropertyName("to_category")]
    public required string ToCategory { get; init; }

    [JsonPropertyName("actor_id")]
    public required Guid ActorId { get; init; }
}

/// <summary>
/// Evento público: oportunidade encerrada como ganha.
/// Consumidores: audit-log, reporting, digest.
/// Sem PII. Money em centavos (long).
/// Mapeia: design §9, Req 14, Req 20, TASK-19.
/// </summary>
public sealed record OpportunityWonV1 : DomainEventEnvelope
{
    public override string EventType => "opportunity.won.v1";

    /// <summary>Valor total em centavos no momento do ganho. Nunca decimal (RNF 11).</summary>
    [JsonPropertyName("valor_total")]
    public required long ValorTotal { get; init; }

    [JsonPropertyName("closed_at")]
    public required DateTimeOffset ClosedAt { get; init; }

    [JsonPropertyName("actor_id")]
    public required Guid ActorId { get; init; }
}

/// <summary>
/// Evento público: oportunidade encerrada como perdida.
/// Consumidores: audit-log, reporting.
/// Sem PII.
/// Mapeia: design §9, Req 10, Req 20, TASK-19.
/// </summary>
public sealed record OpportunityLostV1 : DomainEventEnvelope
{
    public override string EventType => "opportunity.lost.v1";

    [JsonPropertyName("loss_reason_id")]
    public required Guid LossReasonId { get; init; }

    [JsonPropertyName("closed_at")]
    public required DateTimeOffset ClosedAt { get; init; }

    [JsonPropertyName("actor_id")]
    public required Guid ActorId { get; init; }
}

/// <summary>
/// Evento público: oportunidade detectada como estagnada.
/// Consumidores: digest, workflow-automation.
/// Sem PII. Idempotente por (opportunity_id, detection_period).
/// Mapeia: design §9, Req 17, RNF 9, TASK-19.
/// </summary>
public sealed record OpportunityStaleV1 : DomainEventEnvelope
{
    public override string EventType => "opportunity.stale.v1";

    [JsonPropertyName("bu_id")]
    public required Guid BuId { get; init; }

    /// <summary>Última atividade registrada (pode ser null se não houver).</summary>
    [JsonPropertyName("last_activity_at")]
    public DateTimeOffset? LastActivityAt { get; init; }

    [JsonPropertyName("detected_at")]
    public required DateTimeOffset DetectedAt { get; init; }

    /// <summary>Período de detecção (formato "yyyy-MM-dd") — chave de idempotência.</summary>
    [JsonPropertyName("detection_period")]
    public required string DetectionPeriod { get; init; }
}

/// <summary>
/// Evento público: oportunidade reaberta.
/// Consumidores: audit-log, reporting.
/// Snapshot preservado (PBT-11). Sem PII.
/// Mapeia: design §9, Req 15, Req 20, TASK-19.
/// </summary>
public sealed record OpportunityReopenedV1 : DomainEventEnvelope
{
    public override string EventType => "opportunity.reopened.v1";

    /// <summary>Categoria anterior ao reabrir (won ou lost).</summary>
    [JsonPropertyName("previous_category")]
    public required string PreviousCategory { get; init; }

    [JsonPropertyName("actor_id")]
    public required Guid ActorId { get; init; }

    /// <summary>Justificativa da reabertura (opcional, auditada — sem PII).</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

/// <summary>
/// Evento público: comissão de parceiro calculada (projetada).
/// Consumidores: reporting.
/// Money em centavos (long — RNF 11). Sem PII.
/// Mapeia: design §9, Req 12, Req 20, TASK-19.
/// </summary>
public sealed record CommissionCalculatedV1 : DomainEventEnvelope
{
    public override string EventType => "commission.calculated.v1";

    [JsonPropertyName("partner_id")]
    public required Guid PartnerId { get; init; }

    /// <summary>Comissão total calculada em centavos. Nunca decimal (RNF 11).</summary>
    [JsonPropertyName("comissao_total")]
    public required long ComissaoTotal { get; init; }

    /// <summary>Indica se é snapshot (false para projetada).</summary>
    [JsonPropertyName("is_snapshot")]
    public bool IsSnapshot { get; init; }
}

/// <summary>
/// Evento público: snapshot imutável de comissão criado ao ganhar.
/// Consumidores: audit-log, reporting — prioridade Alta (base de pagamento ao parceiro).
/// Money em centavos (long — RNF 11). Sem PII.
/// Mapeia: design §9, Req 14, DD-002, RNF 5, TASK-19.
/// </summary>
public sealed record CommissionSnapshotCreatedV1 : DomainEventEnvelope
{
    public override string EventType => "commission.snapshot_created.v1";

    [JsonPropertyName("partner_id")]
    public required Guid PartnerId { get; init; }

    /// <summary>ID do registro de snapshot no banco.</summary>
    [JsonPropertyName("commission_id")]
    public required Guid CommissionId { get; init; }

    /// <summary>Comissão total do snapshot em centavos. Nunca decimal (RNF 11).</summary>
    [JsonPropertyName("comissao_total")]
    public required long ComissaoTotal { get; init; }

    [JsonPropertyName("snapshot_at")]
    public required DateTimeOffset SnapshotAt { get; init; }
}

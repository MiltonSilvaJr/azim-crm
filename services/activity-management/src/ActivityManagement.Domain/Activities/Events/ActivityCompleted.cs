namespace ActivityManagement.Domain.Activities.Events;

/// <summary>
/// Evento de domínio publicado quando uma atividade é efetivamente concluída
/// (primeira transição para <c>completed</c>).
/// <b>Não carrega title/description</b> — texto livre é PII potencial (RNF 7.2, DD-009).
/// Emitido exatamente uma vez por conclusão efetiva — reconcluir uma atividade já
/// <c>completed</c> é no-op e não acumula novo evento (Req 14.2, Req 14.4, PBT-02, DD-004).
/// Publicado no tópico <c>azim-activities</c> como <c>activity.completed.v1</c>.
/// Mapeia: design §4.4, Req 14.2, TASK-03.
/// </summary>
/// <param name="EventId">UUID único deste evento para deduplicação.</param>
/// <param name="OccurredAt">Instante UTC da ocorrência do evento.</param>
/// <param name="ActivityId">Identificador da atividade concluída.</param>
/// <param name="TenantId">Tenant ao qual a atividade pertence (RNF 1).</param>
/// <param name="OwnerId">Usuário responsável pela atividade.</param>
/// <param name="CompletedAt">Instante UTC da conclusão efetiva.</param>
/// <param name="CorrelationId">Identificador de correlação para rastreabilidade (RNF 6.1).</param>
/// <param name="OpportunityId">Oportunidade vinculada, quando houver.</param>
public sealed record ActivityCompleted(
    Guid            EventId,
    DateTimeOffset  OccurredAt,
    Guid            ActivityId,
    Guid            TenantId,
    Guid            OwnerId,
    DateTimeOffset  CompletedAt,
    Guid            CorrelationId,
    Guid?           OpportunityId)
    : DomainEvent(EventId, OccurredAt);

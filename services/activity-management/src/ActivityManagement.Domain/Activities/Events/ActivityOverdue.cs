namespace ActivityManagement.Domain.Activities.Events;

/// <summary>
/// Evento de domínio publicado quando o scan de vencidas (<c>ScanOverdueActivitiesCommand</c>)
/// detecta uma atividade não terminal com <c>due_at &lt; now()</c>.
/// <b>Não carrega title/description</b> — texto livre é PII potencial (RNF 7.2, DD-009).
/// A deduplicação é feita por chave <c>(activityId, scanDate)</c> no Outbox (DD-005).
/// Publicado no tópico <c>azim-activities</c> como <c>activity.overdue.v1</c>.
/// Mapeia: design §4.4, Req 14.3, DD-005, TASK-03.
/// </summary>
/// <param name="EventId">UUID único deste evento para deduplicação.</param>
/// <param name="OccurredAt">Instante UTC da ocorrência do evento.</param>
/// <param name="ActivityId">Identificador da atividade vencida.</param>
/// <param name="TenantId">Tenant ao qual a atividade pertence (RNF 1).</param>
/// <param name="OwnerId">Usuário responsável pela atividade.</param>
/// <param name="DueAt">Instante de vencimento original da atividade.</param>
/// <param name="ScanDate">Data do scan que originou a detecção; usada na chave de dedup.</param>
/// <param name="CorrelationId">Identificador de correlação para rastreabilidade (RNF 6.1).</param>
/// <param name="OpportunityId">Oportunidade vinculada, quando houver.</param>
public sealed record ActivityOverdue(
    Guid            EventId,
    DateTimeOffset  OccurredAt,
    Guid            ActivityId,
    Guid            TenantId,
    Guid            OwnerId,
    DateTimeOffset  DueAt,
    DateOnly        ScanDate,
    Guid            CorrelationId,
    Guid?           OpportunityId)
    : DomainEvent(EventId, OccurredAt);

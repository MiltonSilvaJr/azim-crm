namespace ActivityManagement.Domain.Activities.Events;

/// <summary>
/// Evento de domínio publicado quando uma atividade é criada com sucesso.
/// <b>Não carrega title/description</b> — texto livre é PII potencial (RNF 7.2, DD-009).
/// Publicado no tópico <c>azim-activities</c> como <c>activity.created.v1</c>.
/// Mapeia: design §4.4, Req 14.1, TASK-03.
/// </summary>
/// <param name="EventId">UUID único deste evento para deduplicação.</param>
/// <param name="OccurredAt">Instante UTC da criação.</param>
/// <param name="ActivityId">Identificador da atividade criada.</param>
/// <param name="TenantId">Tenant ao qual a atividade pertence (RNF 1).</param>
/// <param name="BuId">Business Unit ao qual a atividade pertence.</param>
/// <param name="OwnerId">Usuário responsável pela atividade.</param>
/// <param name="Type">Tipo da atividade (meeting, follow_up, call, email, task).</param>
/// <param name="DueAt">Instante de vencimento da atividade.</param>
/// <param name="CorrelationId">Identificador de correlação para rastreabilidade (RNF 6.1).</param>
/// <param name="OpportunityId">Oportunidade vinculada, quando houver.</param>
/// <param name="AccountId">Conta vinculada, quando houver.</param>
public sealed record ActivityCreated(
    Guid            EventId,
    DateTimeOffset  OccurredAt,
    Guid            ActivityId,
    Guid            TenantId,
    Guid            BuId,
    Guid            OwnerId,
    string          Type,
    DateTimeOffset  DueAt,
    Guid            CorrelationId,
    Guid?           OpportunityId,
    Guid?           AccountId)
    : DomainEvent(EventId, OccurredAt);

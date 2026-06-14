namespace ActivityManagement.Domain.Activities.Events;

/// <summary>
/// Classe base imutável para todos os eventos de domínio do módulo activity-management.
/// Carrega <see cref="EventId"/> (UUID único por evento) e <see cref="OccurredAt"/>
/// (instante da ocorrência em UTC).
/// Eventos de domínio são fatos no passado (rule api-and-contracts.md) e
/// <b>não carregam title/description</b> (RNF 7.2, DD-009).
/// Mapeia: design §4.4, TASK-03.
/// </summary>
/// <summary>
/// Identificador único do evento e instante de ocorrência (campos comuns a todos os eventos).
/// </summary>
/// <param name="EventId">UUID único deste evento; usado para deduplicação (design §6.5).</param>
/// <param name="OccurredAt">Instante UTC em que o evento ocorreu.</param>
public abstract record DomainEvent(Guid EventId, DateTimeOffset OccurredAt);

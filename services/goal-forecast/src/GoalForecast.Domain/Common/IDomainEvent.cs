namespace GoalForecast.Domain.Common;

/// <summary>
/// Marcador para domain events do módulo goal-forecast.
/// Domain events são imutáveis e nomeados no passado.
/// Mapeia: design §4.4.
/// </summary>
public interface IDomainEvent
{
    /// <summary>Identificador único do evento para deduplicação.</summary>
    Guid EventId { get; }

    /// <summary>Momento em que o evento ocorreu (UTC).</summary>
    DateTimeOffset OccurredAt { get; }
}

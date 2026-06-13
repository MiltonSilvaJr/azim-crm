namespace Organization.Domain.Events;

/// <summary>
/// Marcador para domain events enfileirados pelos agregados.
/// Domain events são internos ao domínio e publicados via Outbox (§6.6).
/// Nomeados no passado (ex.: <c>BusinessUnitCreated</c>).
/// </summary>
public interface IDomainEvent
{
    /// <summary>Momento de ocorrência do evento.</summary>
    DateTimeOffset OccurredAt { get; }
}

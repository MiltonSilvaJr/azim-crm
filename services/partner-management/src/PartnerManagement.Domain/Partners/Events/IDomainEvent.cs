namespace PartnerManagement.Domain.Partners.Events;

/// <summary>
/// Marcador para eventos de domínio do módulo partner-management.
/// Eventos são fatos no passado, imutáveis, sem PII em claro (RNF 4, design §4.4).
/// </summary>
public interface IDomainEvent
{
    /// <summary>Identificador único do evento (para deduplicação no consumidor).</summary>
    Guid EventId { get; }

    /// <summary>Momento em que o evento ocorreu (UTC).</summary>
    DateTimeOffset OccurredAt { get; }
}

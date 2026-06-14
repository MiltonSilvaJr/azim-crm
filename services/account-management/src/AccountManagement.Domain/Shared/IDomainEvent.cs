namespace AccountManagement.Domain.Shared;

/// <summary>
/// Marcador para eventos de domínio do módulo account-management.
///
/// Eventos de domínio representam fatos imutáveis que ocorreram no domínio.
/// São acumulados no agregado e despachados após commit via Outbox (design §4.4, DD-007).
/// A carga nunca inclui PII em texto claro (RNF 1.2).
/// </summary>
public interface IDomainEvent
{
    /// <summary>Identificador único do evento (para deduplicação no consumidor — DD-007).</summary>
    Guid EventId { get; }

    /// <summary>Momento em que o evento ocorreu (UTC).</summary>
    DateTimeOffset OccurredAt { get; }
}

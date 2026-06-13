namespace TenantAdministration.Domain.Events;

/// <summary>
/// Marcador de evento de domínio. Todos os domain events implementam esta interface.
/// Eventos são imutáveis e sem dependências de infraestrutura.
/// </summary>
public interface IDomainEvent
{
    /// <summary>Identificador único do evento.</summary>
    Guid EventId { get; }

    /// <summary>Instante em que o evento ocorreu.</summary>
    DateTimeOffset OccurredAt { get; }
}

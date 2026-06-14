using Organization.Domain.Events;

namespace Organization.Domain.Aggregates;

/// <summary>
/// Base para aggregate roots. Mantém a fila de domain events gerados durante a operação.
/// Os eventos são despachados pela camada de Application após persistência bem-sucedida.
/// </summary>
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Domain events enfileirados durante a operação atual (somente leitura).</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Enfileira um domain event. Chamado pelos métodos de comportamento do agregado.
    /// </summary>
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Limpa a fila de domain events após o despacho.
    /// Chamado pela camada de Application após publicar os eventos.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

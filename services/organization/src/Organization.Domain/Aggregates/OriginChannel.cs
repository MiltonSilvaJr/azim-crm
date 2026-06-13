namespace Organization.Domain.Aggregates;

/// <summary>
/// Entidade interna do agregado <see cref="BusinessUnit"/> que representa um canal de origem.
/// </summary>
public sealed class OriginChannel
{
    /// <summary>Identificador único do canal.</summary>
    public Guid Id { get; private set; }

    /// <summary>Nome do canal de origem.</summary>
    public string Name { get; private set; }

    /// <summary>Indica se o canal está ativo.</summary>
    public bool Active { get; private set; }

    private OriginChannel() { Name = string.Empty; }

    internal OriginChannel(Guid id, string name)
    {
        Id = id;
        Name = name;
        Active = true;
    }

    internal void Deactivate() => Active = false;
    internal void Rename(string newName) => Name = newName;
}

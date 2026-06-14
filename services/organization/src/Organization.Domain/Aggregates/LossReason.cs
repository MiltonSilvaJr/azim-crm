namespace Organization.Domain.Aggregates;

/// <summary>
/// Entidade interna do agregado <see cref="BusinessUnit"/> que representa um motivo de perda.
/// A BU exige ao menos um motivo de perda ativo para ser considerada operável
/// (<see cref="Policies.BusinessUnitEnablementSpec"/>).
/// </summary>
public sealed class LossReason
{
    /// <summary>Identificador único do motivo de perda.</summary>
    public Guid Id { get; private set; }

    /// <summary>Nome do motivo de perda.</summary>
    public string Name { get; private set; }

    /// <summary>Indica se o motivo está ativo.</summary>
    public bool Active { get; private set; }

    private LossReason() { Name = string.Empty; }

    internal LossReason(Guid id, string name)
    {
        Id = id;
        Name = name;
        Active = true;
    }

    internal void Deactivate() => Active = false;
    internal void Rename(string newName) => Name = newName;
}

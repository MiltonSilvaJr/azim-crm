using Organization.Domain.ValueObjects;

namespace Organization.Domain.Aggregates;

/// <summary>
/// Entidade interna do agregado <see cref="User"/> que representa o vínculo usuário-BU-papel.
/// Invariante: no máximo um membership por BU por usuário (<c>MembershipUniquenessSpec</c>).
/// </summary>
public sealed class UserMembership
{
    /// <summary>Identificador único do membership.</summary>
    public Guid Id { get; private set; }

    /// <summary>Identificador da Business Unit.</summary>
    public Guid BuId { get; private set; }

    /// <summary>Papel do usuário nesta BU.</summary>
    public Role Role { get; private set; }

    private UserMembership() { Role = ValueObjects.Role.Viewer; }

    internal UserMembership(Guid id, Guid buId, Role role)
    {
        Id = id;
        BuId = buId;
        Role = role;
    }

    internal void ChangeRole(Role newRole)
    {
        Role = newRole;
    }
}

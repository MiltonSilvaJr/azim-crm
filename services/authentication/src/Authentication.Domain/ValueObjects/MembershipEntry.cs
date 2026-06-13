namespace Authentication.Domain.ValueObjects;

/// <summary>
/// Representa uma entrada de membership: unidade de negócio → papel do usuário.
///
/// Imutável por construção (record). Igualdade por valor (bu_id + role).
///
/// Mapeia: design.md § 4.3 (MembershipSet.entries BU → papel).
/// </summary>
public sealed record MembershipEntry
{
    /// <summary>Identificador da unidade de negócio.</summary>
    public Guid BuId { get; }

    /// <summary>Papel do usuário na unidade de negócio (ex.: "admin", "viewer").</summary>
    public string Role { get; }

    /// <summary>
    /// Inicializa uma nova entrada de membership.
    /// </summary>
    /// <param name="buId">Identificador da unidade de negócio (não pode ser <see cref="Guid.Empty"/>).</param>
    /// <param name="role">Papel do usuário (não nulo nem vazio).</param>
    /// <exception cref="ArgumentException">Lançada quando <paramref name="buId"/> é vazio ou <paramref name="role"/> é inválido.</exception>
    public MembershipEntry(Guid buId, string role)
    {
        if (buId == Guid.Empty)
            throw new ArgumentException("BuId não pode ser Guid.Empty.", nameof(buId));
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role não pode ser nulo ou vazio.", nameof(role));

        BuId = buId;
        Role = role;
    }
}

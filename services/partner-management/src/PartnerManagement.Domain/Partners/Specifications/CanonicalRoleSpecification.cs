namespace PartnerManagement.Domain.Partners.Specifications;

/// <summary>
/// Specification: papel informado pertence à lista canônica vigente do tenant.
/// Avalia sobre um <see cref="string"/> (valor do papel), não diretamente sobre um <see cref="Partner"/>,
/// pois é usada na fronteira de validação antes da criação do VO <see cref="ValueObjects.PartnerRole"/>.
/// Mapeia: Req 5.2, Req 5.3, design §4.6, DD-005.
/// </summary>
public sealed class CanonicalRoleSpecification : ISpecification<string>
{
    private readonly IReadOnlyList<string> _canonicalRoles;

    /// <summary>
    /// Inicializa a especificação com a lista canônica vigente do tenant.
    /// </summary>
    /// <param name="canonicalRoles">Lista de papéis canônicos do tenant.</param>
    public CanonicalRoleSpecification(IReadOnlyList<string> canonicalRoles)
    {
        ArgumentNullException.ThrowIfNull(canonicalRoles);
        _canonicalRoles = canonicalRoles;
    }

    /// <inheritdoc/>
    public bool IsSatisfiedBy(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        return _canonicalRoles.Contains(candidate, StringComparer.Ordinal);
    }
}

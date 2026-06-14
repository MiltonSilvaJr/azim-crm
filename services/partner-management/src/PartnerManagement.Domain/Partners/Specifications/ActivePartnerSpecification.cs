using PartnerManagement.Domain.Partners.ValueObjects;

namespace PartnerManagement.Domain.Partners.Specifications;

/// <summary>
/// Specification: parceiro elegível à vinculação com oportunidades.
/// Satisfeita quando <c>partner.Status == Active</c>.
/// Usada pelo pipeline para verificar elegibilidade antes de vincular (Req 8.1, DD-007).
/// Mapeia: Req 4.2, Req 8.1, design §4.6.
/// </summary>
public sealed class ActivePartnerSpecification : ISpecification<Partner>
{
    /// <inheritdoc/>
    public bool IsSatisfiedBy(Partner candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return candidate.Status == PartnerStatus.Active;
    }
}

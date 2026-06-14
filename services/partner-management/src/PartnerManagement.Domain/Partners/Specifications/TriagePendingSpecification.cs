namespace PartnerManagement.Domain.Partners.Specifications;

/// <summary>
/// Specification: parceiro pendente de triagem de percentuais pós-importação.
/// Satisfeita quando <c>pct_setup == 0,00 AND pct_recorrente == 0,00</c>.
/// Derivada — não é flag persistido; ao preencher qualquer percentual > 0 o parceiro
/// deixa de ser pendente automaticamente (Req 11.3, design §4.6).
/// Mapeia: Req 11.1, Req 11.3, design §4.6.
/// </summary>
public sealed class TriagePendingSpecification : ISpecification<Partner>
{
    /// <inheritdoc/>
    public bool IsSatisfiedBy(Partner candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return candidate.CommissionDefaults.IsTriagePending;
    }
}

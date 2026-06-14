namespace PartnerManagement.Domain.Partners.Specifications;

/// <summary>
/// Specification: parceiro pertence ao tenant do contexto corrente.
/// Toda leitura e escrita deve ser restrita ao tenant autenticado (RNF 1, PBT-04, DD-001).
/// Usada como segunda camada de validação de escopo; a primeira é o filtro global EF + RLS.
/// Mapeia: RNF 1, PBT-04, design §4.6.
/// </summary>
public sealed class TenantScopeSpecification : ISpecification<Partner>
{
    private readonly Guid _expectedTenantId;

    /// <summary>
    /// Inicializa a especificação com o tenant do contexto corrente.
    /// </summary>
    /// <param name="expectedTenantId">Identificador do tenant autenticado.</param>
    public TenantScopeSpecification(Guid expectedTenantId)
    {
        _expectedTenantId = expectedTenantId;
    }

    /// <inheritdoc/>
    public bool IsSatisfiedBy(Partner candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return candidate.TenantId == _expectedTenantId;
    }
}

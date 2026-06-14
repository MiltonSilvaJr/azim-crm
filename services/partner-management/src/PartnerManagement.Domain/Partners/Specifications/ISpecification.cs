namespace PartnerManagement.Domain.Partners.Specifications;

/// <summary>
/// Interface genérica para o padrão Specification.
/// Encapsula uma regra de negócio avaliável sobre uma entidade ou valor do domínio.
/// Mapeia: design §4.6.
/// </summary>
/// <typeparam name="T">Tipo sobre o qual a especificação é avaliada.</typeparam>
public interface ISpecification<in T>
{
    /// <summary>Verifica se o item satisfaz a especificação.</summary>
    /// <param name="candidate">Item a ser avaliado.</param>
    /// <returns><c>true</c> se o item satisfaz a regra encapsulada.</returns>
    bool IsSatisfiedBy(T candidate);
}

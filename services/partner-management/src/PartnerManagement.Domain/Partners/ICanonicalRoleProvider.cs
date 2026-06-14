namespace PartnerManagement.Domain.Partners;

/// <summary>
/// Porta de domínio para validação do papel tipado de um parceiro contra a lista canônica vigente do tenant.
/// Implementada na camada Infrastructure (Application/Ports para commands).
/// O domínio não conhece a origem da lista — apenas consulta esta interface.
/// Mapeia: Req 5.2, Req 5.3, design §4.1, DD-005.
/// </summary>
public interface ICanonicalRoleProvider
{
    /// <summary>
    /// Verifica se o papel é canônico para o tenant informado.
    /// </summary>
    /// <param name="role">Papel a ser verificado.</param>
    /// <param name="tenantId">Identificador do tenant.</param>
    /// <returns><c>true</c> se o papel pertence à lista canônica vigente do tenant.</returns>
    bool IsCanonical(string role, Guid tenantId);
}

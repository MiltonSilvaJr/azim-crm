namespace PartnerManagement.Application.Ports;

/// <summary>
/// Porta que expõe o contexto de tenant da requisição corrente.
/// Implementada na Infrastructure via resolução do token JWT (<c>TenantScopeBehavior</c>).
/// Usado pelo <c>TenantScopeBehavior</c> e pelo <c>PartnerManagementDbContext</c> (filtro global).
/// Mapeia: RNF 1, DD-001, design §5.4.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Identificador do tenant autenticado. <c>Guid.Empty</c> se não resolvido.
    /// </summary>
    Guid CurrentTenantId { get; }

    /// <summary>Verdadeiro quando o tenant foi resolvido com sucesso.</summary>
    bool IsResolved => CurrentTenantId != Guid.Empty;
}

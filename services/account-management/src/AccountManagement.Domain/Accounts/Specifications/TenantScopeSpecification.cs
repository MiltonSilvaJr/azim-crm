namespace AccountManagement.Domain.Accounts.Specifications;

/// <summary>
/// Especificação que valida que uma conta pertence ao tenant do contexto atual.
///
/// Garante que toda leitura e escrita seja restrita ao <c>tenant_id</c> autenticado,
/// complementando o filtro global EF Core e o <c>TenantScopeBehavior</c> (defesa em
/// profundidade — DD-002, ADR-0001).
///
/// Mapeia: design §4.6, Req 10, RNF 5.
/// </summary>
public sealed class TenantScopeSpecification
{
    private readonly Guid _tenantId;

    /// <summary>
    /// Inicializa a especificação com o tenant do contexto autenticado.
    /// </summary>
    /// <param name="tenantId">Tenant do contexto atual.</param>
    public TenantScopeSpecification(Guid tenantId)
    {
        _tenantId = tenantId;
    }

    /// <summary>
    /// Retorna <c>true</c> quando <paramref name="account"/> pertence ao tenant do contexto.
    /// </summary>
    /// <param name="account">Conta a avaliar.</param>
    public bool IsSatisfiedBy(Account account) =>
        account.TenantId == _tenantId;
}

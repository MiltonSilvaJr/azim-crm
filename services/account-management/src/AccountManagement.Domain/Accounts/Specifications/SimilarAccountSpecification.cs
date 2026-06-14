using AccountManagement.Domain.Accounts.ValueObjects;

namespace AccountManagement.Domain.Accounts.Specifications;

/// <summary>
/// Especificação que define candidato a duplicata de conta: mesmo <c>tenant_id</c>
/// e <c>normalized_name</c> idêntico ao fornecido.
///
/// Essa especificação é usada pela camada de Application em <c>SearchSimilarAccountsQuery</c>
/// para identificar contas similares antes da criação (alerta não-bloqueante — DD-006).
/// A decisão de criação é do usuário (Req 1.2, Req 1.3).
///
/// Nota: a dedupe é sobre igualdade exata da forma normalizada, não fuzzy matching (DD-005).
///
/// Mapeia: design §4.6, Req 1.2, PBT-02, DD-005, DD-006.
/// </summary>
public sealed class SimilarAccountSpecification
{
    private readonly Guid _tenantId;
    private readonly NormalizedName _normalizedName;

    /// <summary>
    /// Inicializa a especificação com o tenant e a forma normalizada a comparar.
    /// </summary>
    /// <param name="tenantId">Tenant do contexto da busca.</param>
    /// <param name="normalizedName">Forma normalizada do nome a ser comparada.</param>
    public SimilarAccountSpecification(Guid tenantId, NormalizedName normalizedName)
    {
        _tenantId = tenantId;
        _normalizedName = normalizedName;
    }

    /// <summary>
    /// Retorna <c>true</c> quando <paramref name="account"/> é candidato a duplicata:
    /// pertence ao mesmo tenant e possui a mesma forma normalizada do nome.
    /// </summary>
    /// <param name="account">Conta a avaliar.</param>
    public bool IsSatisfiedBy(Account account) =>
        account.TenantId == _tenantId
        && account.NormalizedName == _normalizedName;
}

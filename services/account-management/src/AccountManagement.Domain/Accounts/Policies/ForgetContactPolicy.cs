namespace AccountManagement.Domain.Accounts.Policies;

/// <summary>
/// Policy que determina se um usuário tem permissão para executar o direito ao
/// esquecimento de um contato (LGPD).
///
/// Regra: apenas usuários com o papel <c>TenantAdmin</c> podem acionar o esquecimento.
/// A checagem ocorre na borda de Application (<c>PiiAccessBehavior</c>) e é reforçada
/// aqui no domínio como invariante (Req 7.1, Req 9.3).
///
/// Mapeia: design §4.6, Req 7.1, Req 9.3.
/// </summary>
public sealed class ForgetContactPolicy
{
    /// <summary>
    /// Retorna <c>true</c> quando o papel informado autoriza o esquecimento de contatos.
    /// </summary>
    /// <param name="userRole">Papel do usuário solicitante.</param>
    public bool IsAllowed(string? userRole) =>
        string.Equals(userRole, "TenantAdmin", StringComparison.OrdinalIgnoreCase);
}

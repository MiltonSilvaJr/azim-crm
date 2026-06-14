namespace AccountManagement.Domain.Accounts.Policies;

/// <summary>
/// Policy que determina se um usuário tem permissão para acessar PII de contatos.
///
/// Regra: papel mínimo <c>Vendedor</c> (na BU relacionada à conta) para leitura de PII.
/// A checagem ocorre na borda de Application (<c>PiiAccessBehavior</c>) e é reforçada
/// aqui no domínio (Req 9.1, RNF 6).
///
/// Hierarquia de papéis (crescente de permissão): Viewer → Vendedor → TenantAdmin.
///
/// Mapeia: design §4.6, Req 9.1, RNF 6.
/// </summary>
public sealed class PiiAccessPolicy
{
    private static readonly HashSet<string> AllowedRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Vendedor", "TenantAdmin" };

    /// <summary>
    /// Retorna <c>true</c> quando o papel informado autoriza o acesso à PII de contatos.
    /// </summary>
    /// <param name="userRole">Papel do usuário solicitante.</param>
    public bool CanAccessPii(string? userRole) =>
        userRole is not null && AllowedRoles.Contains(userRole);
}

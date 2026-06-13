namespace Authentication.Domain.Specifications;

/// <summary>
/// Specification que verifica se o tenant do token corresponde ao tenant resolvido pelo slug.
///
/// Regra (design.md § 4.6, Req 4.3, PBT-02):
///   <c>firebase.tenant</c> do token == tenant de identidade do tenant resolvido pelo slug.
///   Divergência → 401 AUTH-ERR-004.
///
/// Esta verificação previne que um token válido de tenant A seja aceito em contexto de tenant B
/// (isolamento multi-tenant — PBT-02, Req 4.3, design.md § 10).
///
/// Pure function — sem efeito colateral.
///
/// Mapeia: Req 4.3, design.md § 4.6, PBT-02.
/// </summary>
public static class TenantMatchSpec
{
    /// <summary>
    /// Avalia se o <c>firebase_tenant</c> do token corresponde ao tenant esperado.
    /// </summary>
    /// <param name="firebaseTenant">Identificador do tenant do IdP extraído do token JWT.</param>
    /// <param name="expectedFirebaseTenant">Identificador do tenant de identidade esperado (resolvido pelo slug).</param>
    /// <returns>
    /// <see langword="true"/> quando os valores são iguais (não nulos e correspondentes);
    /// <see langword="false"/> em qualquer outro caso (incluindo nulo, vazio ou divergência).
    /// </returns>
    public static bool IsSatisfiedBy(string firebaseTenant, string expectedFirebaseTenant)
    {
        if (string.IsNullOrEmpty(firebaseTenant) || string.IsNullOrEmpty(expectedFirebaseTenant))
            return false;

        return string.Equals(firebaseTenant, expectedFirebaseTenant, StringComparison.Ordinal);
    }
}

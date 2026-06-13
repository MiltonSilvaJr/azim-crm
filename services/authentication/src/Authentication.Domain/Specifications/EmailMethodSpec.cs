namespace Authentication.Domain.Specifications;

/// <summary>
/// Specification que verifica se o método de autenticação do usuário é compatível
/// com o fluxo de recuperação de senha.
///
/// Regra (design.md § 4.6, Req 8.4):
///   Usuário autenticado via Google (ou qualquer provedor não-password) não deve
///   receber opção de recuperação de senha — a senha não existe para esses usuários.
///   Somente o método <c>"password"</c> (Firebase email/senha) é compatível.
///
/// Pure function — sem efeito colateral.
///
/// Mapeia: Req 8.4, design.md § 4.6.
/// </summary>
public static class EmailMethodSpec
{
    /// <summary>
    /// Identificador do método password do Firebase Authentication.
    /// </summary>
    private const string PasswordProvider = "password";

    /// <summary>
    /// Avalia se o <paramref name="signInProvider"/> é compatível com recuperação de senha.
    /// </summary>
    /// <param name="signInProvider">
    /// Identificador do provedor de autenticação (ex.: <c>"password"</c>, <c>"google.com"</c>).
    /// </param>
    /// <returns>
    /// <see langword="true"/> somente quando <paramref name="signInProvider"/> é <c>"password"</c>.
    /// </returns>
    public static bool IsSatisfiedBy(string signInProvider) =>
        string.Equals(signInProvider, PasswordProvider, StringComparison.OrdinalIgnoreCase);
}

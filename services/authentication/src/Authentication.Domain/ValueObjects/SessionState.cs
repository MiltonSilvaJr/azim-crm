namespace Authentication.Domain.ValueObjects;

/// <summary>
/// Estados possíveis de uma sessão no módulo authentication.
///
/// Máquina de estados (design.md § 4.5):
///   [*] → Anonymous
///   Anonymous → Authenticated (token válido escopado ao tenant)
///   Authenticated → Expired (TTL atingido)
///   Authenticated → Revoked (logout ou revogação global)
///   Expired → [*] (estado terminal — exige novo token)
///   Revoked → [*] (estado terminal — exige novo login)
///
/// Mapeia: Req 4.2, Req 9.4, design.md § 4.5.
/// </summary>
public enum SessionState
{
    /// <summary>
    /// Sessão sem token válido; usuário não autenticado.
    /// Acesso a recursos protegidos negado.
    /// </summary>
    Anonymous = 0,

    /// <summary>
    /// Token válido, tenant verificado, usuário ativo.
    /// Acesso concedido enquanto o token não expirar.
    /// </summary>
    Authenticated = 1,

    /// <summary>
    /// Token com TTL esgotado. Estado terminal.
    /// Novo token válido necessário (renovação via refresh token no frontend).
    /// </summary>
    Expired = 2,

    /// <summary>
    /// Sessão revogada explicitamente (logout ou revogação global do IdP).
    /// Estado terminal. Novo login necessário.
    /// </summary>
    Revoked = 3,
}

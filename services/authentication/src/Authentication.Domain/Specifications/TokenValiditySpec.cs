using Authentication.Domain.ValueObjects;

namespace Authentication.Domain.Specifications;

/// <summary>
/// Specification que verifica se um token de sessão é válido para acesso.
///
/// Regra (design.md § 4.6, Req 4.2):
///   Token presente, bem-formado, assinatura válida (verificada pelo JWKS),
///   não expirado e não revogado.
///
/// Nesta camada de domínio, a especificação opera sobre o objeto de valor <see cref="Session"/>
/// já produzido pelo validator (que valida assinatura e JWKS externamente).
/// Aqui verificamos: estado <see cref="SessionState.Authenticated"/> e token não expirado.
///
/// Pure function — sem efeito colateral.
///
/// Mapeia: Req 4.2, design.md § 4.6.
/// </summary>
public static class TokenValiditySpec
{
    /// <summary>
    /// Avalia se a sessão satisfaz os critérios de validade de token.
    /// </summary>
    /// <param name="session">Sessão a ser avaliada.</param>
    /// <param name="now">Momento UTC corrente para avaliação de expiração.</param>
    /// <returns>
    /// <see langword="true"/> quando o estado é <see cref="SessionState.Authenticated"/>
    /// e o token não está expirado no momento <paramref name="now"/>.
    /// </returns>
    public static bool IsSatisfiedBy(Session session, DateTimeOffset now) =>
        session.IsAccessible(now);
}

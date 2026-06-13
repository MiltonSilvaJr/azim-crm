using Authentication.Application.Ports.Results;

namespace Authentication.Application.Ports;

/// <summary>
/// Porta de saída que abstrai a verificação local de assinatura de tokens JWT via JWKS.
///
/// Implementada por <c>JwksTokenVerifier</c> em Infrastructure, que mantém cache das
/// chaves públicas do IdP e valida a assinatura sem round-trip ao IdP por requisição
/// (RNF 2, DD-002).
///
/// Mapeia: Req 4.5, RNF 2, design.md § 6.4, DD-002.
/// </summary>
public interface ITokenVerifier
{
    /// <summary>
    /// Verifica localmente a assinatura, issuer, audience e lifetime do JWT.
    ///
    /// Não verifica revogação (round-trip ao IdP); essa verificação é realizada
    /// pelo <see cref="IIdentityProvider"/> quando necessário.
    ///
    /// Em caso de token inválido, lança <see cref="Exceptions.IdentityProviderException"/>
    /// com código correspondente (AUTH-ERR-001, 002 ou 003).
    /// </summary>
    /// <param name="rawJwt">Token JWT bruto.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Resultado com dados do token verificado.</returns>
    /// <exception cref="Exceptions.IdentityProviderException">Lançada quando o token é inválido.</exception>
    Task<VerifyTokenResult> VerifySignatureAsync(
        string rawJwt,
        CancellationToken cancellationToken = default);
}

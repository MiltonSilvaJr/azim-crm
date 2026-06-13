using Authentication.Application.Ports;
using Authentication.Application.Ports.Exceptions;
using Authentication.Application.Ports.Results;
using Authentication.Domain.Specifications;
using Authentication.Domain.ValueObjects;

namespace Authentication.Application.Services;

/// <summary>
/// Serviço de aplicação responsável pela validação de um token JWT de sessão.
///
/// Orquestra:
///   1. <see cref="IIdentityProvider.VerifyTokenAsync"/> — valida assinatura, lifetime e tenant.
///   2. <see cref="TenantMatchSpec"/> — verifica que o <c>firebase_tenant</c> do token
///      corresponde ao tenant resolvido pelo slug.
///
/// Em caso de sucesso, retorna a <see cref="Session"/> no estado <c>Authenticated</c>
/// e o <see cref="VerifyTokenResult"/> para consumo pelo <see cref="AuthContextComposer"/>.
///
/// Token com firebase_tenant divergente → <see cref="IdentityProviderException"/> AUTH-ERR-004.
///
/// Não contém <c>identity_uid</c> — o <see cref="VerifyTokenResult.ProviderUserRef"/> é opaco (DD-001).
///
/// Mapeia: TASK-06, design.md § 5.3, Req 4, PBT-02.
/// </summary>
public sealed class SessionTokenValidator
{
    private readonly IIdentityProvider _identityProvider;

    /// <summary>
    /// Inicializa o validador com o provedor de identidade.
    /// </summary>
    /// <param name="identityProvider">Porta de saída do provedor de identidade.</param>
    public SessionTokenValidator(IIdentityProvider identityProvider)
    {
        _identityProvider = identityProvider;
    }

    /// <summary>
    /// Valida o JWT bruto e retorna a sessão autenticada com o resultado de verificação.
    /// </summary>
    /// <param name="rawJwt">Token JWT bruto extraído do header Authorization.</param>
    /// <param name="expectedFirebaseTenant">
    /// Tenant de identidade esperado, resolvido pelo <c>TenantResolutionMiddleware</c>.
    /// </param>
    /// <param name="tenantId">UUID interno do tenant resolvido pelo slug.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// Tupla com a <see cref="Session"/> autenticada e o <see cref="VerifyTokenResult"/>
    /// para uso pelo <see cref="AuthContextComposer"/>.
    /// </returns>
    /// <exception cref="IdentityProviderException">
    /// Lançada quando o token é inválido (AUTH-ERR-001/002/003) ou quando o tenant
    /// do token diverge do esperado (AUTH-ERR-004, PBT-02).
    /// </exception>
    public async Task<(Session session, VerifyTokenResult verifyResult)> ValidateAsync(
        string rawJwt,
        string expectedFirebaseTenant,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // Delega verificação de assinatura, lifetime e tenant ao IdP
        var verifyResult = await _identityProvider.VerifyTokenAsync(
            rawJwt,
            expectedFirebaseTenant,
            cancellationToken);

        // Aplica TenantMatchSpec: firebase_tenant do token deve corresponder ao esperado (PBT-02)
        if (!TenantMatchSpec.IsSatisfiedBy(verifyResult.FirebaseTenant, expectedFirebaseTenant))
        {
            throw new IdentityProviderException(
                "AUTH-ERR-004",
                "Token de tenant divergente do contexto da requisição (PBT-02, Req 4.3).");
        }

        // Constrói sessão autenticada com o tenant resolvido pelo slug
        var session = Session.Create(
            state: SessionState.Authenticated,
            tenantId: tenantId,
            issuedAt: DateTimeOffset.UtcNow,
            expiresAt: DateTimeOffset.UtcNow.AddHours(1), // TTL padrão do Identity Platform (DD-008)
            revocationChecked: true);

        return (session, verifyResult);
    }
}

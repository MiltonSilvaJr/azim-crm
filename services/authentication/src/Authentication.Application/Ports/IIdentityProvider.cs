using Authentication.Application.Ports.Results;

namespace Authentication.Application.Ports;

/// <summary>
/// Porta de saída que abstrai toda interação com o provedor de identidade (GCP Identity Platform).
///
/// É o único ponto de acoplamento ao IdP externo. A substituição do provedor impacta
/// apenas o adapter em <c>Authentication.Infrastructure</c> (Req 6, DD-001).
///
/// Nenhum tipo do Firebase Admin SDK e nenhum símbolo <c>identity_uid</c> é exposto
/// nesta interface (DD-001, Req 6.2, 6.3).
///
/// Contrato (design.md § 6.4):
///   - <c>VerifyTokenAsync</c>   — verifica assinatura, lifetime e tenant do JWT
///   - <c>RevokeRefreshTokensAsync</c> — logout global (idempotente, PBT-04)
///   - <c>GenerateInviteActivationAsync</c> — gera link de ativação (TTL DD-009)
///   - <c>GeneratePasswordResetLinkAsync</c> — gera link de redefinição de senha
///   - <c>HealthCheckAsync</c>   — verifica disponibilidade do IdP (RNF 3.2)
///
/// Mapeia: Req 6, Req 6.4, design.md § 6.4, DD-001.
/// </summary>
public interface IIdentityProvider
{
    /// <summary>
    /// Verifica o JWT bruto e retorna a referência de identidade do usuário.
    ///
    /// Valida: assinatura (via JWKS), lifetime, issuer, audience e tenant de identidade.
    /// O <c>expectedFirebaseTenant</c> é o tenant de identidade resolvido pelo slug
    /// e deve corresponder ao claim <c>firebase.tenant</c> do token.
    ///
    /// Em caso de falha, lança <see cref="IdentityProviderException"/> com o código
    /// do catálogo de erros correspondente (Req 6.5).
    /// </summary>
    /// <param name="rawJwt">Token JWT bruto extraído do header Authorization.</param>
    /// <param name="expectedFirebaseTenant">Tenant de identidade esperado (resolvido pelo slug).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Resultado contendo os dados de identidade do usuário verificado.</returns>
    /// <exception cref="IdentityProviderException">
    /// Lançada em qualquer falha do IdP (token inválido, expirado, tenant incorreto, etc.).
    /// </exception>
    Task<VerifyTokenResult> VerifyTokenAsync(
        string rawJwt,
        string expectedFirebaseTenant,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoga todos os refresh tokens do usuário, encerrando todas as sessões ativas.
    ///
    /// Operação idempotente: revogar sessão já revogada produz o mesmo estado final
    /// sem lançar exceção (Req 9.5, PBT-04).
    ///
    /// O parâmetro é o identificador interno do usuário no tenant (não o identity_uid externo).
    /// O adapter em Infrastructure é responsável por resolver o identity_uid correspondente.
    /// </summary>
    /// <param name="userIdInTenant">Identificador interno do usuário no tenant Azim.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task RevokeRefreshTokensAsync(
        Guid userIdInTenant,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gera um link de ativação de convite com o TTL configurado (DD-009, padrão 72h).
    ///
    /// Falhas de envio não devem ser tratadas aqui; o link é retornado para que
    /// o <c>IEmailSender</c> seja invocado pelo serviço de aplicação.
    /// </summary>
    /// <param name="email">E-mail do usuário convidado.</param>
    /// <param name="firebaseTenant">Tenant de identidade no qual o usuário será criado.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Link de ativação gerado (URL + metadados de expiração).</returns>
    /// <exception cref="IdentityProviderException">Lançada em falha do IdP.</exception>
    Task<ActivationLinkResult> GenerateInviteActivationAsync(
        string email,
        string firebaseTenant,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gera um link de redefinição de senha para o usuário com o e-mail fornecido.
    ///
    /// Deve ser chamado apenas quando <c>EmailMethodSpec</c> for satisfeita
    /// (usuário de método password). Para e-mails Google ou inexistentes, o serviço
    /// de aplicação não deve chamar este método (anti-enumeração, Req 8, PBT-03).
    /// </summary>
    /// <param name="email">E-mail do usuário.</param>
    /// <param name="firebaseTenant">Tenant de identidade.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Link de redefinição gerado.</returns>
    /// <exception cref="IdentityProviderException">Lançada em falha do IdP.</exception>
    Task<ResetLinkResult> GeneratePasswordResetLinkAsync(
        string email,
        string firebaseTenant,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica a disponibilidade do provedor de identidade.
    ///
    /// Usado em health checks de readiness (RNF 3.2).
    /// Nunca lança exceção: retorna <see cref="HealthStatus.Unhealthy"/> em falha.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Status de saúde do provedor.</returns>
    Task<HealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default);
}

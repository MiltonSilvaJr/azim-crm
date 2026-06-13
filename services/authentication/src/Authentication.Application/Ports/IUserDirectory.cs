using Authentication.Application.Ports.Results;
using Authentication.Domain.ValueObjects;

namespace Authentication.Application.Ports;

/// <summary>
/// Porta de saída que abstrai o acesso ao diretório de usuários do módulo <c>organization</c>.
///
/// Resolve a tradução do token opaco de identidade para o <c>user_id</c> interno
/// e carrega os memberships do usuário no tenant. Nunca expõe <c>identity_uid</c>
/// ao chamador (DD-001).
///
/// Mapeia: Req 5, design.md § 6.1, DD-001.
/// </summary>
public interface IUserDirectory
{
    /// <summary>
    /// Resolve o usuário interno a partir do token opaco de identidade e tenant.
    ///
    /// Retorna <see langword="null"/> quando nenhum usuário ativo é encontrado
    /// para o token de identidade no tenant informado.
    /// </summary>
    /// <param name="providerUserRef">Referência opaca ao provedor de identidade (não exposta além da Application).</param>
    /// <param name="tenantId">UUID do tenant no qual o usuário deve ser pesquisado.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// Resultado com <c>user_id</c>, e-mail, roles e memberships, ou
    /// <see langword="null"/> quando não encontrado/inativo.
    /// </returns>
    Task<UserDirectoryResult?> FindUserAsync(
        string providerUserRef,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se um e-mail já está associado a um usuário ativo no tenant.
    ///
    /// Usado pelo <c>InviteActivationService</c> para detectar e-mail duplicado
    /// sem expor dados do usuário existente (Req 7.6, AUTH-ERR-030).
    /// </summary>
    /// <param name="email">E-mail a verificar.</param>
    /// <param name="tenantId">UUID do tenant.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns><see langword="true"/> quando o e-mail já está ativo no tenant.</returns>
    Task<bool> IsEmailActiveAsync(
        string email,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve o usuário a partir do e-mail no tenant.
    ///
    /// Usado pelo <c>PasswordResetService</c> para verificar o método de autenticação
    /// sem revelar existência de conta (anti-enumeração — PBT-03, Req 10.2).
    ///
    /// Retorna <see langword="null"/> quando o e-mail não existe ou está inativo.
    /// </summary>
    /// <param name="email">E-mail do usuário.</param>
    /// <param name="tenantId">UUID do tenant.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// Resultado com dados do usuário (incluindo <c>SignInProvider</c>),
    /// ou <see langword="null"/> quando não encontrado.
    /// </returns>
    Task<UserDirectoryResult?> FindUserByEmailAsync(
        string email,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

using Digest.Application.Models;

namespace Digest.Application.Ports;

/// <summary>
/// Porta de leitura somente-leitura de preferências de opt-out de digest por usuário.
/// Fonte: módulo organization (DD-003, Req 10, VAL-DIGEST-04).
/// O digest apenas lê; a posse do dado é do organization.
/// Implementação concreta vive em <c>Digest.Infrastructure</c>.
/// </summary>
/// <remarks>
/// Restrições:
/// <list type="bullet">
///   <item>Toda consulta é restrita ao <c>tenant_id</c> (Req 6.4).</item>
///   <item>Retorna preferência com <c>OptOut = false</c> (padrão inclusivo) quando não há registro.</item>
///   <item>Sem escrita — leitura pura (Req 6.2).</item>
/// </list>
/// </remarks>
public interface IUserDigestPreferencePort
{
    /// <summary>
    /// Retorna a preferência de opt-out do digest para um usuário específico.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant. Obrigatório.</param>
    /// <param name="userId">Identificador do usuário. Obrigatório.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// Preferência do usuário. Quando não há registro, retorna <see cref="DigestPreference"/>
    /// com <c>OptOut = false</c> (padrão inclusivo).
    /// </returns>
    Task<DigestPreference> GetPreferenceAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna as preferências de opt-out de todos os usuários do tenant em lote.
    /// Otimização: evita N+1 queries por usuário (RNF 4.3).
    /// </summary>
    /// <param name="tenantId">Identificador do tenant. Obrigatório.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Lista de preferências; nunca <see langword="null"/>.</returns>
    Task<IReadOnlyList<DigestPreference>> GetAllPreferencesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

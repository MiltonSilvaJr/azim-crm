using Digest.Domain.Enums;
using Digest.Domain.ValueObjects;

namespace Digest.Application.Ports;

/// <summary>
/// Porta de emissão de tokens de ação de um clique.
/// Produz um <see cref="ActionToken"/> (par token-em-claro + hash) por atividade.
/// Implementação concreta (<c>ActionTokenFactory</c>) vive em <c>Digest.Infrastructure</c> (TASK-16).
/// </summary>
/// <remarks>
/// O token em claro (<see cref="ActionToken.ClearToken"/>) é incluído no link do e-mail;
/// somente o <see cref="ActionToken.TokenHash"/> é persistido em <c>digest_action_tokens</c> (DD-007).
/// A entropia mínima é 256 bits via CSPRNG (RNF 7.2, PBT-05).
/// </remarks>
public interface IActionTokenFactory
{
    /// <summary>
    /// Emite um <see cref="ActionToken"/> para uma atividade específica.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant. Obrigatório.</param>
    /// <param name="userId">Identificador do usuário destinatário. Obrigatório.</param>
    /// <param name="activityId">Identificador da atividade. Obrigatório.</param>
    /// <param name="action">Tipo de ação (<see cref="ActionType.Complete"/> ou <see cref="ActionType.Reschedule"/>).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// <see cref="ActionToken"/> com o token em claro (para o link) e o hash (para persistência).
    /// O token em claro existe apenas em memória durante a emissão — nunca logar (DD-011, RNF 3).
    /// </returns>
    Task<ActionToken> IssueAsync(
        Guid tenantId,
        Guid userId,
        Guid activityId,
        ActionType action,
        CancellationToken cancellationToken = default);
}

using Digest.Domain.Entities;
using Digest.Domain.Enums;
using Digest.Domain.ValueObjects;

namespace Digest.Application.Repositories;

/// <summary>
/// Repositório de <see cref="DigestActionToken"/> — tokens de ação de um clique.
/// Implementação concreta em <c>Digest.Infrastructure</c> (TASK-17).
/// </summary>
/// <remarks>
/// Persiste apenas o hash SHA-256 do token em claro (DD-007).
/// O token em claro nunca passa pela camada de persistência.
/// </remarks>
public interface IDigestActionTokenRepository
{
    /// <summary>
    /// Persiste o <see cref="DigestActionToken"/> emitido para uma atividade.
    /// Somente o <see cref="DigestActionToken.TokenHash"/> é armazenado (DD-007).
    /// </summary>
    /// <param name="token">Entidade com hash e metadados. Token em claro não incluído.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task IssueTokenAsync(
        DigestActionToken token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Localiza um <see cref="DigestActionToken"/> pelo hash SHA-256.
    /// Retorna <c>null</c> quando não encontrado ou expirado.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant (Global Query Filter).</param>
    /// <param name="tokenHash">Hash SHA-256 do token.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<DigestActionToken?> GetByHashAsync(
        Guid tenantId,
        byte[] tokenHash,
        CancellationToken cancellationToken = default);
}

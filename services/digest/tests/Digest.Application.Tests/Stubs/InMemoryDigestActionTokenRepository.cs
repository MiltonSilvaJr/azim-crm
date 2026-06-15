using Digest.Application.Repositories;
using Digest.Domain.Entities;

namespace Digest.Application.Tests.Stubs;

/// <summary>
/// Stub in-memory de <see cref="IDigestActionTokenRepository"/> para testes de Application.
/// Armazena tokens emitidos para verificação em testes.
/// </summary>
public sealed class InMemoryDigestActionTokenRepository : IDigestActionTokenRepository
{
    private readonly List<DigestActionToken> _tokens = [];

    /// <summary>Tokens emitidos durante o teste.</summary>
    public IReadOnlyList<DigestActionToken> IssuedTokens => _tokens.AsReadOnly();

    /// <inheritdoc/>
    public Task IssueTokenAsync(DigestActionToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        _tokens.Add(token);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<DigestActionToken?> GetByHashAsync(
        Guid tenantId,
        byte[] tokenHash,
        CancellationToken cancellationToken = default)
    {
        var found = _tokens.FirstOrDefault(t =>
            t.TenantId == tenantId && t.TokenHash.SequenceEqual(tokenHash));
        return Task.FromResult<DigestActionToken?>(found);
    }
}

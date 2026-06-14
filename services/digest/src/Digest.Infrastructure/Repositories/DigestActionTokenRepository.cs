using Digest.Application.Repositories;
using Digest.Domain.Entities;
using Digest.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Digest.Infrastructure.Repositories;

/// <summary>
/// Implementação de <see cref="IDigestActionTokenRepository"/> usando EF Core 9 e PostgreSQL.
/// Persiste somente o hash SHA-256 do token; o token em claro nunca é armazenado (DD-007).
/// Protegida por Global Query Filter (ADR-0001) e RLS falha-fechada (TASK-15).
/// </summary>
public sealed class DigestActionTokenRepository : IDigestActionTokenRepository
{
    private readonly DigestDbContext _db;

    /// <summary>
    /// Constrói o repositório com o DbContext injetado.
    /// </summary>
    public DigestActionTokenRepository(DigestDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task IssueTokenAsync(
        DigestActionToken token,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        // EF Core rastreia e persiste a entidade — token_hash é BYTEA (UNIQUE)
        // O token em claro não consta na entidade e nunca é logado (DD-007, DD-011)
        _db.DigestActionTokens.Add(token);
        await _db.SaveChangesAsync(cancellationToken);

        // Detach para evitar rastreamento desnecessário pós-persistência
        _db.Entry(token).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
    }

    /// <inheritdoc/>
    public Task<DigestActionToken?> GetByHashAsync(
        Guid tenantId,
        byte[] tokenHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        if (tokenHash.Length != 32)
            throw new ArgumentException("TokenHash deve ter 32 bytes (SHA-256).", nameof(tokenHash));

        // Global Query Filter garante que somente o tenant correto é consultado
        // EF Core compara BYTEA nativamente via Npgsql
        return _db.DigestActionTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.TenantId == tenantId && t.TokenHash == tokenHash,
                cancellationToken);
    }
}

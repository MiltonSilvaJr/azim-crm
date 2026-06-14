namespace ActivityManagement.Infrastructure.Tokens;

using ActivityManagement.Application.Ports;
using ActivityManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Implementação de <see cref="IDigestActionTokenPort"/> que consulta e consome
/// <c>digest_action_tokens</c> via EF Core com Global Query Filter de tenant ativo.
///
/// Design:
/// - Consulta exclusivamente por <c>token_hash</c> — o token em claro nunca transita aqui.
/// - Retorna <c>null</c> quando o token não existe ou é de outro tenant (anti-enumeração — Req 7.6).
/// - <see cref="MarkUsedAsync"/> usa UPDATE com cláusula <c>WHERE used_at IS NULL</c>
///   garantindo idempotência e uso único sem condição de corrida.
/// - RLS do PostgreSQL + Global Query Filter formam as duas camadas de isolamento (ADR-0001).
///
/// Mapeia: TASK-17, DD-003, RNF 5, design §6.4, Req 7.
/// </summary>
internal sealed class DigestActionTokenAdapter : IDigestActionTokenPort
{
    private readonly ActivityManagementDbContext _db;

    /// <summary>Inicializa o adapter com o contexto EF corrente.</summary>
    public DigestActionTokenAdapter(ActivityManagementDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<DigestActionTokenData?> FindByHashAsync(
        string            tokenHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        // Global Query Filter garante que apenas tokens do tenant ativo são retornados.
        // Retorna null para tokens de outro tenant (indistinguível de inexistente — Req 7.6).
        var token = await _db.DigestActionTokens
            .AsNoTracking()
            .Where(t => t.TokenHash == tokenHash)
            .Select(t => new DigestActionTokenData(
                t.Id,
                t.TenantId,
                t.UserId,
                t.ActivityId,
                t.Action,
                t.ExpiresAt,
                t.UsedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return token;
    }

    /// <inheritdoc/>
    public async Task MarkUsedAsync(
        Guid              tokenId,
        DateTimeOffset    usedAt,
        CancellationToken cancellationToken = default)
    {
        // UPDATE direto com WHERE used_at IS NULL — garante idempotência e uso único
        // sem necessidade de leitura prévia (evita condição de corrida).
        // Usa ExecuteUpdateAsync (EF Core 7+) para UPDATE sem carregar a entidade.
        await _db.DigestActionTokens
            .Where(t => t.Id == tokenId && t.UsedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(t => t.UsedAt, usedAt),
                cancellationToken);
    }
}

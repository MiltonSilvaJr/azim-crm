using Digest.Infrastructure.Clock;
using Digest.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Digest.Infrastructure.Jobs;

/// <summary>
/// Job de retenção e purge de dados de digest (TASK-24, RNF 9).
/// Responsabilidades:
/// <list type="bullet">
///   <item>Remover <c>email_digest_logs</c> com <c>scheduled_at</c> mais antigo que 90 dias (RNF 9.1).</item>
///   <item>Remover <c>digest_action_tokens</c> com <c>expires_at &lt; now()</c> (RNF 9.2).</item>
/// </list>
/// Cada operação respeita RLS — o contexto de tenant deve estar setado antes de chamar os métodos.
/// Log de purge registra somente contagem por tenant, sem PII (RNF 9.3, DD-011).
/// </summary>
public sealed class DigestRetentionPurgeJob
{
    private readonly DigestDbContext _db;
    private readonly IClock _clock;

    /// <summary>
    /// Constrói o job de purge com o DbContext e relógio injetados.
    /// </summary>
    /// <param name="db">DbContext do módulo digest (com Global Query Filter por tenant_id).</param>
    /// <param name="clock">Abstração do relógio (injetável para testes).</param>
    public DigestRetentionPurgeJob(DigestDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>
    /// Remove registros de <c>email_digest_logs</c> do tenant com <c>scheduled_at</c> mais antigo
    /// que 90 dias (RNF 9.1). Opera dentro do escopo do tenant informado.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant a purgar.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Número de registros removidos.</returns>
    public async Task<int> PurgeDigestLogsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenant_id não pode ser vazio.", nameof(tenantId));

        // Cutoff: 90 dias atrás (RNF 9.1)
        var cutoff = _clock.UtcNow.AddDays(-90);

        // DELETE direto via SQL para eficiência; parâmetros interpolados (FormattableString) são seguros contra injeção.
        // O filtro de tenant_id garante isolamento mesmo que o Global Query Filter não esteja ativo nesta query.
        // Log registra somente contagem — sem e-mail, nome ou conteúdo (RNF 9.3, DD-011).
        var deleted = await _db.Database.ExecuteSqlAsync(
            $"""
             DELETE FROM email_digest_logs
             WHERE tenant_id = {tenantId}
               AND scheduled_at < {cutoff}
             """,
            cancellationToken);

        return deleted;
    }

    /// <summary>
    /// Remove tokens de <c>digest_action_tokens</c> do tenant com <c>expires_at</c> anterior ao
    /// momento atual (RNF 9.2). Opera dentro do escopo do tenant informado.
    /// Tokens com <c>used_at</c> não nulo e <c>expires_at</c> passado também são removidos.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant a purgar.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Número de tokens removidos.</returns>
    public async Task<int> PurgeExpiredTokensAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenant_id não pode ser vazio.", nameof(tenantId));

        var now = _clock.UtcNow;

        // Remove tokens expirados — inclusive os já utilizados (used_at não nulo) pós-expiração
        // Log somente com contagem, sem PII (DD-011)
        var deleted = await _db.Database.ExecuteSqlAsync(
            $"""
             DELETE FROM digest_action_tokens
             WHERE tenant_id = {tenantId}
               AND expires_at < {now}
             """,
            cancellationToken);

        return deleted;
    }
}

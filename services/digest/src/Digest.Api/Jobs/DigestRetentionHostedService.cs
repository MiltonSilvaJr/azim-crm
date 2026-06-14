using Digest.Infrastructure.Clock;
using Digest.Infrastructure.Jobs;
using Digest.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Digest.Api.Jobs;

/// <summary>
/// Hosted service que executa o purge de retenção diariamente às 02:00 UTC (TASK-24, RNF 9).
/// Itera por tenant (respeitando RLS via <see cref="DigestRetentionPurgeJob"/>) e remove:
/// <list type="bullet">
///   <item><c>email_digest_logs</c> com mais de 90 dias (RNF 9.1).</item>
///   <item><c>digest_action_tokens</c> expirados (RNF 9.2).</item>
/// </list>
/// Log registra somente contagem por tenant, sem PII (RNF 9.3, DD-011).
/// </summary>
public sealed class DigestRetentionHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DigestRetentionHostedService> _logger;

    /// <summary>
    /// Constrói o hosted service com factory de escopo (serviços scoped em background service).
    /// </summary>
    public DigestRetentionHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<DigestRetentionHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Aguarda até a próxima janela de 02:00 UTC
        await WaitUntilNextRunAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunPurgeAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    // ---------------------------------------------------------------
    // Lógica de purge (internal para permitir invocação em testes de smoke)
    // ---------------------------------------------------------------

    /// <summary>
    /// Executa o purge em todos os tenants. Falha de um tenant não interrompe os demais.
    /// Log sem PII: somente tenant_id (UUID opaco) e contagem (DD-011, RNF 9.3).
    /// </summary>
    internal async Task RunPurgeAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Iniciando job de purge de retenção do digest.");

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DigestDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        // Obtém lista de tenants únicos com dados (cross-tenant — IgnoreQueryFilters para listar todos os tenants)
        // O purge per-tenant garante isolamento via WHERE tenant_id = @id na query SQL.
        var tenantIds = await db.EmailDigestLogs
            .IgnoreQueryFilters()
            .Select(l => l.TenantId)
            .Distinct()
            .Union(
                db.DigestActionTokens
                    .IgnoreQueryFilters()
                    .Select(t => t.TenantId)
                    .Distinct())
            .ToListAsync(cancellationToken);

        foreach (var tenantId in tenantIds)
        {
            if (cancellationToken.IsCancellationRequested) break;
            try
            {
                var job = new DigestRetentionPurgeJob(db, clock);
                var logsPurged = await job.PurgeDigestLogsAsync(tenantId, cancellationToken);
                var tokensPurged = await job.PurgeExpiredTokensAsync(tenantId, cancellationToken);

                // Log sem PII: apenas tenant_id (UUID) e contagem (RNF 9.3, DD-011)
                _logger.LogInformation(
                    "Purge concluído para tenant {TenantId}: {LogsPurged} logs e {TokensPurged} tokens removidos.",
                    tenantId, logsPurged, tokensPurged);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Falha em um tenant não interrompe os demais
                _logger.LogError(ex, "Falha no purge para tenant {TenantId}.", tenantId);
            }
        }

        _logger.LogInformation("Job de purge de retenção do digest concluído.");
    }

    // ---------------------------------------------------------------
    // Helper: calcular próxima execução às 02:00 UTC
    // ---------------------------------------------------------------

    private static async Task WaitUntilNextRunAsync(CancellationToken stoppingToken)
    {
        var now = DateTimeOffset.UtcNow;
        var next = new DateTimeOffset(now.Year, now.Month, now.Day, 2, 0, 0, TimeSpan.Zero);

        if (next <= now)
            next = next.AddDays(1);

        var delay = next - now;
        if (delay > TimeSpan.Zero && !stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(delay, stoppingToken);
        }
    }
}

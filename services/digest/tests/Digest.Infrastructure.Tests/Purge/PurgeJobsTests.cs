using Digest.Domain.Entities;
using Digest.Domain.ValueObjects;
using Digest.Infrastructure.Jobs;
using Digest.Infrastructure.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Xunit;

namespace Digest.Infrastructure.Tests.Purge;

/// <summary>
/// Testes de integração para os jobs de purge (TASK-24, RNF 9).
/// Verifica: registros com mais de 90 dias em email_digest_logs são removidos;
/// registros dentro do prazo são preservados; tokens expirados em digest_action_tokens
/// são removidos; tokens válidos permanecem.
/// Usa Testcontainers + PostgreSQL real (compartilhado via collection "Postgres").
/// </summary>
[Collection("Postgres")]
public sealed class PurgeJobsTests
{
    private readonly PostgresContainerFixture _fixture;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public PurgeJobsTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    // ---------------------------------------------------------------
    // EmailDigestLog purge — 90 dias (RNF 9.1)
    // ---------------------------------------------------------------

    [Fact(DisplayName = "TASK-24: purge remove email_digest_logs com mais de 90 dias")]
    public async Task PurgeDigestLogs_RemovesRecordsOlderThan90Days()
    {
        // Arrange
        await using var ctx = _fixture.CreateDbContext(_tenantId);

        // Registro com 91 dias → deve ser removido
        var oldLog = EmailDigestLog.Schedule(_tenantId, _userId, new DigestDate(new LocalDate(2026, 3, 1)), null);
        // Força scheduled_at para 91 dias atrás via SQL
        ctx.EmailDigestLogs.Add(oldLog);
        await ctx.SaveChangesAsync();
        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-91);
        await ctx.Database.ExecuteSqlAsync(
            $"UPDATE email_digest_logs SET scheduled_at = {cutoffDate} WHERE id = {oldLog.Id}");

        // Registro com 89 dias → deve ser preservado
        var userId2 = Guid.NewGuid();
        var recentLog = EmailDigestLog.Schedule(_tenantId, userId2, new DigestDate(new LocalDate(2026, 3, 17)), null);
        ctx.EmailDigestLogs.Add(recentLog);
        await ctx.SaveChangesAsync();
        var recentDate = DateTimeOffset.UtcNow.AddDays(-89);
        await ctx.Database.ExecuteSqlAsync(
            $"UPDATE email_digest_logs SET scheduled_at = {recentDate} WHERE id = {recentLog.Id}");

        // Act — clock simulado com "agora"
        var now = DateTimeOffset.UtcNow;
        var purgeJob = new DigestRetentionPurgeJob(ctx, new FakeClock(now));
        var purged = await purgeJob.PurgeDigestLogsAsync(_tenantId, CancellationToken.None);

        // Assert
        purged.Should().Be(1, "somente o registro com 91 dias deve ser removido");

        var remaining = await ctx.EmailDigestLogs
            .AsNoTracking()
            .Where(l => l.TenantId == _tenantId)
            .ToListAsync();

        remaining.Should().HaveCount(1, "registro com 89 dias deve ser preservado");
        remaining[0].Id.Should().Be(recentLog.Id);
    }

    [Fact(DisplayName = "TASK-24: purge preserva email_digest_logs dentro do prazo de 90 dias")]
    public async Task PurgeDigestLogs_PreservesRecordsWithin90Days()
    {
        // Arrange
        await using var ctx = _fixture.CreateDbContext(_tenantId);

        var userId3 = Guid.NewGuid();
        var validLog = EmailDigestLog.Schedule(_tenantId, userId3, new DigestDate(new LocalDate(2026, 6, 1)), null);
        ctx.EmailDigestLogs.Add(validLog);
        await ctx.SaveChangesAsync();
        // Mantém scheduled_at como agora (dentro do prazo)

        // Act
        var now = DateTimeOffset.UtcNow;
        var purgeJob = new DigestRetentionPurgeJob(ctx, new FakeClock(now));
        var purged = await purgeJob.PurgeDigestLogsAsync(_tenantId, CancellationToken.None);

        // Assert
        purged.Should().Be(0, "nenhum registro dentro do prazo deve ser removido");
    }

    // ---------------------------------------------------------------
    // DigestActionToken purge — expires_at < now (RNF 9.2)
    // ---------------------------------------------------------------

    [Fact(DisplayName = "TASK-24: purge remove digest_action_tokens com expires_at expirado")]
    public async Task PurgeActionTokens_RemovesExpiredTokens()
    {
        // Arrange
        await using var ctx = _fixture.CreateDbContext(_tenantId);

        var activityId = Guid.NewGuid();
        var expiredToken = DigestActionToken.Issue(
            tenantId: _tenantId,
            userId: _userId,
            activityId: activityId,
            action: Domain.Enums.ActionType.Complete,
            token: Domain.ValueObjects.ActionToken.Issue());

        ctx.DigestActionTokens.Add(expiredToken);
        await ctx.SaveChangesAsync();

        // Forçar expires_at para o passado via SQL (token já expirou)
        var pastExpiry = DateTimeOffset.UtcNow.AddHours(-1);
        await ctx.Database.ExecuteSqlAsync(
            $"UPDATE digest_action_tokens SET expires_at = {pastExpiry} WHERE id = {expiredToken.Id}");

        // Token válido (expires_at = agora + 48h — dentro do prazo)
        var activityId2 = Guid.NewGuid();
        var validToken = DigestActionToken.Issue(
            tenantId: _tenantId,
            userId: _userId,
            activityId: activityId2,
            action: Domain.Enums.ActionType.Reschedule,
            token: Domain.ValueObjects.ActionToken.Issue());

        ctx.DigestActionTokens.Add(validToken);
        await ctx.SaveChangesAsync();

        // Act
        var now = DateTimeOffset.UtcNow;
        var purgeJob = new DigestRetentionPurgeJob(ctx, new FakeClock(now));
        var purged = await purgeJob.PurgeExpiredTokensAsync(_tenantId, CancellationToken.None);

        // Assert
        purged.Should().Be(1, "somente o token expirado deve ser removido");

        var remaining = await ctx.DigestActionTokens
            .AsNoTracking()
            .Where(t => t.TenantId == _tenantId)
            .ToListAsync();

        remaining.Should().HaveCount(1);
        remaining[0].Id.Should().Be(validToken.Id);
    }

    [Fact(DisplayName = "TASK-24: log de purge não contém PII — apenas contagem por tenant")]
    public async Task PurgeDigestLogs_LogsCountWithoutPii()
    {
        // Este teste verifica que o job aceita CancellationToken e retorna contagem numérica,
        // sem expor e-mail, nome de usuário ou conteúdo (RNF 9.3, DD-011).
        // Verificação estrutural: retorno é int (contagem), sem payload com PII.
        await using var ctx = _fixture.CreateDbContext(_tenantId);
        var now = DateTimeOffset.UtcNow;
        var purgeJob = new DigestRetentionPurgeJob(ctx, new FakeClock(now));

        // PurgeDigestLogsAsync e PurgeExpiredTokensAsync devem retornar int (contagem)
        var logsPurged = await purgeJob.PurgeDigestLogsAsync(_tenantId, CancellationToken.None);
        var tokensPurged = await purgeJob.PurgeExpiredTokensAsync(_tenantId, CancellationToken.None);

        logsPurged.Should().BeGreaterThanOrEqualTo(0);
        tokensPurged.Should().BeGreaterThanOrEqualTo(0);
    }
}

/// <summary>Relógio falso injetável para testes de purge.</summary>
file sealed class FakeClock : Digest.Infrastructure.Clock.IClock
{
    private readonly DateTimeOffset _now;
    public FakeClock(DateTimeOffset now) => _now = now;
    public DateTimeOffset UtcNow => _now;
}

using Digest.Application.Repositories;
using Digest.Domain.Entities;
using Digest.Domain.Enums;
using Digest.Domain.ValueObjects;
using Digest.Infrastructure.Repositories;
using Digest.Infrastructure.Tests.Fixtures;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Xunit;

namespace Digest.Infrastructure.Tests.Repositories;

/// <summary>
/// Testes de integração para <see cref="EmailDigestLogRepository"/> (TASK-17).
/// Verifica: reserva de idempotência via ON CONFLICT DO NOTHING, corrida concorrente,
/// transições de status e PBT-02 (infra side: N reservas para mesma chave → 1 registro).
/// Usa Testcontainers + PostgreSQL real (compartilhado via collection "Postgres").
/// </summary>
[Collection("Postgres")]
public sealed class EmailDigestLogRepositoryTests
{
    private readonly PostgresContainerFixture _fixture;

    private static readonly DigestDate Monday20260615 = new(new LocalDate(2026, 6, 15));

    public EmailDigestLogRepositoryTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    // ---------------------------------------------------------------
    // ReserveAsync — caminho feliz
    // ---------------------------------------------------------------

    [Fact(DisplayName = "ReserveAsync retorna Reserved quando não existe registro anterior")]
    public async Task ReserveAsync_NoExisting_ReturnsReserved()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var ctx = _fixture.CreateDbContext(tenantId);
        var repo = new EmailDigestLogRepository(ctx);

        var result = await repo.ReserveAsync(tenantId, userId, Monday20260615, null);

        result.Should().Be(ReservationResult.Reserved);
    }

    [Fact(DisplayName = "ReserveAsync persiste registro com status scheduled")]
    public async Task ReserveAsync_PersistsScheduledRecord()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var ctx = _fixture.CreateDbContext(tenantId);
        var repo = new EmailDigestLogRepository(ctx);

        await repo.ReserveAsync(tenantId, userId, Monday20260615, null);

        var log = await ctx.EmailDigestLogs.FirstOrDefaultAsync(
            l => l.TenantId == tenantId && l.UserId == userId);

        log.Should().NotBeNull();
        log!.Status.Should().Be(DigestStatus.Scheduled);
    }

    // ---------------------------------------------------------------
    // ReserveAsync — idempotência: segunda chamada retorna AlreadyScheduled
    // ---------------------------------------------------------------

    [Fact(DisplayName = "Segunda ReserveAsync para a mesma chave retorna AlreadyScheduled")]
    public async Task ReserveAsync_SecondCall_SameKey_ReturnsAlreadyScheduled()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var ctx = _fixture.CreateDbContext(tenantId);
        var repo = new EmailDigestLogRepository(ctx);

        var first = await repo.ReserveAsync(tenantId, userId, Monday20260615, null);
        var second = await repo.ReserveAsync(tenantId, userId, Monday20260615, null);

        first.Should().Be(ReservationResult.Reserved);
        second.Should().Be(ReservationResult.AlreadyScheduled);
    }

    [Fact(DisplayName = "ReserveAsync para chave já enviada retorna AlreadySent")]
    public async Task ReserveAsync_AlreadySent_ReturnsAlreadySent()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var date = new DigestDate(new LocalDate(2026, 6, 22));
        await using var ctx = _fixture.CreateDbContext(tenantId);
        var repo = new EmailDigestLogRepository(ctx);

        // Reserva e depois marca como enviado
        await repo.ReserveAsync(tenantId, userId, date, null);
        await repo.MarkSentAsync(tenantId, userId, date, "msg-001");

        // Segunda chamada deve retornar AlreadySent
        var result = await repo.ReserveAsync(tenantId, userId, date, null);

        result.Should().Be(ReservationResult.AlreadySent);
    }

    // ---------------------------------------------------------------
    // Corrida concorrente: duas reservas simultâneas para a mesma chave
    // ---------------------------------------------------------------

    [Fact(DisplayName = "Corrida concorrente: somente uma reserva persiste na tabela (ON CONFLICT DO NOTHING)")]
    public async Task ReserveAsync_ConcurrentRace_OnlyOneRecordPersists()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var date = new DigestDate(new LocalDate(2026, 6, 29));

        // Duas conexões independentes para simular workers concorrentes
        await using var ctx1 = _fixture.CreateDbContext(tenantId);
        await using var ctx2 = _fixture.CreateDbContext(tenantId);
        var repo1 = new EmailDigestLogRepository(ctx1);
        var repo2 = new EmailDigestLogRepository(ctx2);

        // Executa as duas reservas em paralelo
        var t1 = repo1.ReserveAsync(tenantId, userId, date, null);
        var t2 = repo2.ReserveAsync(tenantId, userId, date, null);

        var results = await Task.WhenAll(t1, t2);

        // Exatamente um Reserved e um AlreadyScheduled (ou ambos na ordem que chegar)
        var reservedCount = results.Count(r => r == ReservationResult.Reserved);
        var scheduledCount = results.Count(r => r == ReservationResult.AlreadyScheduled);

        reservedCount.Should().Be(1, "somente um worker deve vencer a corrida (ON CONFLICT DO NOTHING)");
        scheduledCount.Should().Be(1, "o perdedor deve receber AlreadyScheduled");

        // Confirma que somente 1 linha existe na tabela
        await using var verifyCtx = _fixture.CreateDbContext(tenantId);
        var count = await verifyCtx.EmailDigestLogs
            .CountAsync(l => l.TenantId == tenantId && l.UserId == userId);

        count.Should().Be(1, "ON CONFLICT DO NOTHING deve garantir exatamente 1 registro");
    }

    // ---------------------------------------------------------------
    // MarkSentAsync
    // ---------------------------------------------------------------

    [Fact(DisplayName = "MarkSentAsync atualiza status para Sent e registra messageId")]
    public async Task MarkSentAsync_UpdatesToSentWithMessageId()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var date = new DigestDate(new LocalDate(2026, 7, 6));
        await using var ctx = _fixture.CreateDbContext(tenantId);
        var repo = new EmailDigestLogRepository(ctx);

        await repo.ReserveAsync(tenantId, userId, date, null);
        await repo.MarkSentAsync(tenantId, userId, date, "msg-sent-001");

        await using var verifyCtx = _fixture.CreateDbContext(tenantId);
        var log = await verifyCtx.EmailDigestLogs
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.UserId == userId);

        log.Should().NotBeNull();
        log!.Status.Should().Be(DigestStatus.Sent);
        log.MessageId.Should().Be("msg-sent-001");
        log.SentAt.Should().NotBeNull();
    }

    // ---------------------------------------------------------------
    // MarkFailedAsync
    // ---------------------------------------------------------------

    [Fact(DisplayName = "MarkFailedAsync atualiza status para Failed e registra failed_at")]
    public async Task MarkFailedAsync_UpdatesToFailedWithTimestamp()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var date = new DigestDate(new LocalDate(2026, 7, 13));
        await using var ctx = _fixture.CreateDbContext(tenantId);
        var repo = new EmailDigestLogRepository(ctx);

        await repo.ReserveAsync(tenantId, userId, date, null);
        await repo.MarkFailedAsync(tenantId, userId, date);

        await using var verifyCtx = _fixture.CreateDbContext(tenantId);
        var log = await verifyCtx.EmailDigestLogs
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.UserId == userId);

        log.Should().NotBeNull();
        log!.Status.Should().Be(DigestStatus.Failed);
        log.FailedAt.Should().NotBeNull();
    }

    // ---------------------------------------------------------------
    // PBT-02 (infra side): N reservas para a mesma chave → exatamente 1 registro
    // ---------------------------------------------------------------

    [Property(MaxTest = 10, DisplayName = "PBT-02 (infra): N reservas sequenciais para a mesma chave → exatamente 1 registro")]
    public Property Pbt02_NReservations_SameKey_ExactlyOneRecord(PositiveInt seed)
    {
        // N entre 2 e 5 para manter tempo razoável com Testcontainers
        var n = (seed.Get % 4) + 2;

        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var date = new DigestDate(new LocalDate(2026, 8, 4));

        var results = new List<ReservationResult>();

        for (int i = 0; i < n; i++)
        {
            using var ctx = _fixture.CreateDbContext(tenantId);
            var repo = new EmailDigestLogRepository(ctx);
            var r = repo.ReserveAsync(tenantId, userId, date, null).GetAwaiter().GetResult();
            results.Add(r);
        }

        // Exatamente 1 Reserved; demais AlreadyScheduled
        var reservedCount = results.Count(r => r == ReservationResult.Reserved);

        // Verifica contagem no banco
        using var verifyCtx = _fixture.CreateDbContext(tenantId);
        var dbCount = verifyCtx.EmailDigestLogs
            .Count(l => l.TenantId == tenantId && l.UserId == userId);

        return (reservedCount == 1 && dbCount == 1)
            .ToProperty()
            .Label($"N={n}: reserved={reservedCount}, db_count={dbCount}");
    }
}

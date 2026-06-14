using Digest.Domain.Events;
using Digest.Domain.ValueObjects;
using Digest.Infrastructure.Messaging;
using Digest.Infrastructure.Repositories;
using Digest.Infrastructure.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Xunit;

namespace Digest.Infrastructure.Tests.Messaging;

/// <summary>
/// Testes de integração para <see cref="OutboxPublisher"/> (TASK-18).
/// Verifica atomicidade: UPDATE sent + INSERT outbox_messages na mesma transação (DD-009).
/// Usa Testcontainers + PostgreSQL real (compartilhado via collection "Postgres").
/// </summary>
[Collection("Postgres")]
public sealed class OutboxPublisherTests
{
    private readonly PostgresContainerFixture _fixture;
    private static readonly DigestDate TestDate = new(new LocalDate(2026, 6, 14));

    public OutboxPublisherTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    // ---------------------------------------------------------------
    // Publicação nominal
    // ---------------------------------------------------------------

    [Fact(DisplayName = "PublishAsync insere registro em outbox_messages com payload sem PII")]
    public async Task PublishAsync_InsertsOutboxMessage_WithNoPii()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var messageId = "msg-outbox-001";
        var occurredAt = DateTimeOffset.UtcNow;

        var domainEvent = new DigestEmailSent(
            TenantId: tenantId,
            UserId: userId,
            DigestDate: TestDate,
            MessageId: messageId,
            OccurredAt: occurredAt);

        await using var ctx = _fixture.CreateDbContext(tenantId);
        var publisher = new OutboxPublisher(ctx);

        // Act
        await publisher.PublishAsync(domainEvent);

        // Assert: verifica via contexto separado (sem Global Query Filter em OutboxMessages)
        await using var verifyCtx = _fixture.CreateDbContextWithoutTenant();
        var message = await verifyCtx.OutboxMessages
            .FirstOrDefaultAsync(m => m.TenantId == tenantId);

        message.Should().NotBeNull();
        message!.EventType.Should().Be("DigestEmailSent");
        message.TenantId.Should().Be(tenantId);
        message.ProcessedAt.Should().BeNull("mensagem recém inserida ainda não foi processada");

        // Payload não deve conter campos de PII (sem e-mail ou conteúdo — RNF 10.2)
        message.Payload.Should().NotContain("email",
            "payload do outbox não deve conter PII (RNF 10.2)");
        message.Payload.Should().Contain(tenantId.ToString());
        message.Payload.Should().Contain(userId.ToString());
        message.Payload.Should().Contain(messageId);
        message.Payload.Should().Contain("2026-06-14"); // digest_date serializado como ISO
    }

    // ---------------------------------------------------------------
    // Atomicidade: UPDATE sent + INSERT outbox_messages na mesma transação
    // ---------------------------------------------------------------

    [Fact(DisplayName = "UPDATE sent + PublishAsync na mesma transação — ambos persistem juntos")]
    public async Task MarkSent_And_Publish_InSameTransaction_BothPersist()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var date = new DigestDate(new LocalDate(2026, 7, 7));
        var messageId = "msg-atomic-001";

        // Setup: reserva
        await using var setupCtx = _fixture.CreateDbContext(tenantId);
        var repo = new EmailDigestLogRepository(setupCtx);
        await repo.ReserveAsync(tenantId, userId, date, null);

        // Act: UPDATE sent + INSERT outbox dentro da mesma conexão/transação
        await using var ctx = _fixture.CreateDbContext(tenantId);
        await using var transaction = await ctx.Database.BeginTransactionAsync();

        var txRepo = new EmailDigestLogRepository(ctx);
        var publisher = new OutboxPublisher(ctx);

        await txRepo.MarkSentAsync(tenantId, userId, date, messageId);

        var domainEvent = new DigestEmailSent(
            TenantId: tenantId,
            UserId: userId,
            DigestDate: date,
            MessageId: messageId,
            OccurredAt: DateTimeOffset.UtcNow);

        await publisher.PublishAsync(domainEvent);
        await transaction.CommitAsync();

        // Assert: ambos os registros devem estar presentes
        await using var verifyCtx = _fixture.CreateDbContext(tenantId);
        var log = await verifyCtx.EmailDigestLogs
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.UserId == userId);

        log.Should().NotBeNull();
        log!.MessageId.Should().Be(messageId);

        await using var outboxCtx = _fixture.CreateDbContextWithoutTenant();
        var outboxMsg = await outboxCtx.OutboxMessages
            .FirstOrDefaultAsync(m => m.TenantId == tenantId);

        outboxMsg.Should().NotBeNull("INSERT outbox deve ter persistido junto com o UPDATE sent");
        outboxMsg!.MessageId().Should().Be(messageId);
    }

    [Fact(DisplayName = "Rollback da transação desfaz tanto o UPDATE sent quanto o INSERT outbox")]
    public async Task Rollback_UndoesBoth_UpdateSent_And_OutboxInsert()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var date = new DigestDate(new LocalDate(2026, 7, 14));
        var messageId = "msg-rollback-001";

        // Setup: reserva
        await using var setupCtx = _fixture.CreateDbContext(tenantId);
        var repo = new EmailDigestLogRepository(setupCtx);
        await repo.ReserveAsync(tenantId, userId, date, null);

        // Act: inicia transação, faz UPDATE + INSERT, depois ROLLBACK
        await using var ctx = _fixture.CreateDbContext(tenantId);
        await using var transaction = await ctx.Database.BeginTransactionAsync();

        var txRepo = new EmailDigestLogRepository(ctx);
        var publisher = new OutboxPublisher(ctx);

        await txRepo.MarkSentAsync(tenantId, userId, date, messageId);

        var domainEvent = new DigestEmailSent(
            TenantId: tenantId,
            UserId: userId,
            DigestDate: date,
            MessageId: messageId,
            OccurredAt: DateTimeOffset.UtcNow);

        await publisher.PublishAsync(domainEvent);

        // ROLLBACK — simula falha após UPDATE + INSERT
        await transaction.RollbackAsync();

        // Assert: UPDATE deve ter sido desfeito — status ainda é scheduled
        await using var verifyCtx = _fixture.CreateDbContext(tenantId);
        var log = await verifyCtx.EmailDigestLogs
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.UserId == userId);

        log.Should().NotBeNull();
        log!.Status.Should().Be(Domain.Enums.DigestStatus.Scheduled,
            "rollback deve ter desfeito o UPDATE sent (DD-009)");
        log.MessageId.Should().BeNull("rollback deve ter desfeito o message_id");

        // INSERT outbox deve ter sido desfeito também
        await using var outboxCtx = _fixture.CreateDbContextWithoutTenant();
        var outboxMsg = await outboxCtx.OutboxMessages
            .FirstOrDefaultAsync(m => m.TenantId == tenantId);

        outboxMsg.Should().BeNull("rollback deve ter desfeito o INSERT no outbox (DD-009)");
    }

    [Fact(DisplayName = "Payload do outbox contém os campos obrigatórios sem PII")]
    public async Task PublishAsync_Payload_ContainsRequiredFields_NoPii()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var messageId = "msg-payload-001";

        var domainEvent = new DigestEmailSent(
            TenantId: tenantId,
            UserId: userId,
            DigestDate: new DigestDate(new LocalDate(2026, 6, 14)),
            MessageId: messageId,
            OccurredAt: DateTimeOffset.UtcNow);

        await using var ctx = _fixture.CreateDbContext(tenantId);
        var publisher = new OutboxPublisher(ctx);
        await publisher.PublishAsync(domainEvent);

        await using var verifyCtx = _fixture.CreateDbContextWithoutTenant();
        var message = await verifyCtx.OutboxMessages.FirstOrDefaultAsync(m => m.TenantId == tenantId);

        message.Should().NotBeNull();

        // Verifica campos presentes no JSON
        var payload = message!.Payload;
        payload.Should().Contain("tenant_id");
        payload.Should().Contain("user_id");
        payload.Should().Contain("digest_date");
        payload.Should().Contain("message_id");
        payload.Should().Contain("occurred_at");

        // Verifica ausência de campos de PII
        payload.Should().NotContain("email", "payload não deve ter campo de e-mail (RNF 10.2)");
        payload.Should().NotContain("name", "payload não deve ter nome do usuário (RNF 10.2)");
        payload.Should().NotContain("content", "payload não deve ter conteúdo do digest (RNF 10.2)");
    }
}

// ---------------------------------------------------------------------------
// Extension para leitura do message_id do payload JSON (helper de teste)
// ---------------------------------------------------------------------------

internal static class OutboxMessageExtensions
{
    internal static string? MessageId(this OutboxMessage message)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(message.Payload);
        return doc.RootElement.TryGetProperty("message_id", out var prop) ? prop.GetString() : null;
    }
}

using AccountManagement.Domain.Accounts.Events;
using AccountManagement.Domain.Accounts.ValueObjects;
using AccountManagement.Infrastructure.Audit;
using AccountManagement.Infrastructure.Outbox;
using AccountManagement.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AccountManagement.Infrastructure.Tests.Outbox;

/// <summary>
/// Testes de integração para <see cref="OutboxEventPublisher"/> com PostgreSQL real.
///
/// Cobre TASK-11 (ST-03):
/// - Grava mensagem de Outbox com EventType correto para cada tipo de evento.
/// - Payload gravado não contém PII em texto claro (mascaramento pelo PiiMasker — DD-003).
/// - PublishedAt é null ao gravar (relay publica assincronamente — DD-007).
/// - TenantId correto no envelope da mensagem.
///
/// Mapeia: TASK-11 (ST-03), DD-003, DD-007, RNF 1.2.
/// </summary>
[Collection("PostgresFixture")]
public sealed class OutboxEventPublisherTests
{
    private readonly PostgresFixture _fixture;
    private readonly PiiMasker _piiMasker = new();

    public OutboxEventPublisherTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // =========================================================================
    // AccountCreated — EventType e TenantId corretos
    // =========================================================================

    [Fact]
    public async Task PublishAsync_AccountCreated_writes_outbox_message_with_correct_event_type()
    {
        var tenantId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var domainEvent = new AccountCreated(
            EventId: Guid.NewGuid(),
            AccountId: accountId,
            TenantId: tenantId,
            NormalizedName: "EMPRESA OUTBOX",
            OccurredAt: DateTimeOffset.UtcNow);

        await using var ctx = _fixture.CreateDbContext(tenantId);
        var publisher = new OutboxEventPublisher(ctx, _piiMasker);

        await publisher.PublishAsync(domainEvent);
        await ctx.SaveChangesAsync(); // TransactionBehavior chama SaveChanges em runtime

        var message = await ReadOutboxMessageAsync(tenantId);
        message.Should().NotBeNull("deve existir mensagem no outbox");
        message!.EventType.Should().Be("account.created.v1");
        message.TenantId.Should().Be(tenantId);
        message.PublishedAt.Should().BeNull("PublishedAt só é preenchido pelo relay após confirmação do broker");
    }

    [Fact]
    public async Task PublishAsync_AccountUpdated_writes_outbox_message_with_correct_event_type()
    {
        var tenantId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var domainEvent = new AccountUpdated(
            EventId: Guid.NewGuid(),
            AccountId: accountId,
            TenantId: tenantId,
            ChangedFields: ["name"],
            OccurredAt: DateTimeOffset.UtcNow);

        await using var ctx = _fixture.CreateDbContext(tenantId);
        var publisher = new OutboxEventPublisher(ctx, _piiMasker);

        await publisher.PublishAsync(domainEvent);
        await ctx.SaveChangesAsync();

        var message = await ReadOutboxMessageAsync(tenantId);
        message.Should().NotBeNull();
        message!.EventType.Should().Be("account.updated.v1");
    }

    [Fact]
    public async Task PublishAsync_ContactLinked_writes_outbox_message_with_correct_event_type()
    {
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var domainEvent = new ContactLinked(
            EventId: Guid.NewGuid(),
            ContactId: contactId,
            AccountId: Guid.NewGuid(),
            TenantId: tenantId,
            Action: "created",
            MaskedDelta: """{"name":"[anonimizado]"}""",
            OccurredAt: DateTimeOffset.UtcNow);

        await using var ctx = _fixture.CreateDbContext(tenantId);
        var publisher = new OutboxEventPublisher(ctx, _piiMasker);

        await publisher.PublishAsync(domainEvent);
        await ctx.SaveChangesAsync();

        var message = await ReadOutboxMessageAsync(tenantId);
        message.Should().NotBeNull();
        message!.EventType.Should().Be("account.contact_linked.v1");
    }

    [Fact]
    public async Task PublishAsync_ContactForgotten_writes_outbox_message_with_correct_event_type()
    {
        var tenantId = Guid.NewGuid();

        var domainEvent = new ContactForgotten(
            EventId: Guid.NewGuid(),
            ContactId: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            TenantId: tenantId,
            RequestedBy: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow);

        await using var ctx = _fixture.CreateDbContext(tenantId);
        var publisher = new OutboxEventPublisher(ctx, _piiMasker);

        await publisher.PublishAsync(domainEvent);
        await ctx.SaveChangesAsync();

        var message = await ReadOutboxMessageAsync(tenantId);
        message.Should().NotBeNull();
        message!.EventType.Should().Be("account.contact_forgotten.v1");
    }

    // =========================================================================
    // Gate anti-PII: PayloadJson nunca contém PII em texto claro
    // =========================================================================

    [Fact]
    public async Task PublishAsync_ContactLinked_payload_json_in_db_never_contains_clear_pii()
    {
        var tenantId = Guid.NewGuid();

        // MaskedDelta com PII — PiiMasker deve mascarar ao gravar no Outbox
        const string piiName = "Vazamento PII Teste";
        const string piiEmail = "vazamento@pii.com";
        var maskedDelta = $$"""{"name":"{{piiName}}","email":"{{piiEmail}}","role":"tester"}""";

        var domainEvent = new ContactLinked(
            EventId: Guid.NewGuid(),
            ContactId: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            TenantId: tenantId,
            Action: "created",
            MaskedDelta: maskedDelta,
            OccurredAt: DateTimeOffset.UtcNow);

        await using var ctx = _fixture.CreateDbContext(tenantId);
        var publisher = new OutboxEventPublisher(ctx, _piiMasker);

        await publisher.PublishAsync(domainEvent);
        await ctx.SaveChangesAsync();

        var message = await ReadOutboxMessageAsync(tenantId);
        message.Should().NotBeNull();

        _piiMasker.ContainsPii(message!.PayloadJson, [piiName, piiEmail])
            .Should().BeFalse(
                "PayloadJson do Outbox nunca pode conter PII em texto claro (gate DD-003, RNF 1.2)");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private async Task<OutboxMessageDto?> ReadOutboxMessageAsync(Guid tenantId)
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT event_type, tenant_id, payload_json, published_at
            FROM outbox_messages
            WHERE tenant_id = '{tenantId}'
            ORDER BY occurred_at DESC
            LIMIT 1";

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new OutboxMessageDto(
            EventType: reader.GetString(0),
            TenantId: reader.GetGuid(1),
            PayloadJson: reader.GetString(2),
            PublishedAt: reader.IsDBNull(3) ? null : reader.GetDateTime(3));
    }

    private sealed record OutboxMessageDto(
        string EventType,
        Guid TenantId,
        string PayloadJson,
        DateTime? PublishedAt);
}

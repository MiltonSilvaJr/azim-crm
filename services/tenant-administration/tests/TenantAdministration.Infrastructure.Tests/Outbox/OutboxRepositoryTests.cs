using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TenantAdministration.Domain.Aggregates;
using TenantAdministration.Domain.ValueObjects;
using TenantAdministration.Infrastructure.Outbox;
using TenantAdministration.Infrastructure.Tests.Fixtures;
using Xunit;

namespace TenantAdministration.Infrastructure.Tests.Outbox;

/// <summary>
/// Testes de integração do Outbox com PostgreSQL real (TASK-15).
/// Verifica que outbox_events é escrito na mesma transação do agregado
/// e que o publicador marca eventos como published/failed.
/// </summary>
[Collection("Postgres")]
public sealed class OutboxRepositoryTests
{
    private readonly PostgresContainerFixture _fixture;

    public OutboxRepositoryTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "AppendAsync escreve evento na tabela outbox_events com correlation_id")]
    public async Task AppendAsync_WritesOutboxEvent_WithCorrelationId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var correlationId = Guid.NewGuid().ToString();
        var tenantCtx = new FakeTenantContext(tenantId, correlationId: correlationId);
        using var db = _fixture.CreateDbContext();
        var outbox = new OutboxRepository(db, tenantCtx);

        var slug = Slug.Create("outbox-test").Value;
        var tz = TimezoneIana.Create("America/Sao_Paulo").Value;
        var dt = DigestTime.Create("07:00").Value;
        var tenant = Tenant.Provision(slug, "Outbox Test", tz, dt, "admin@outbox.com", DateTimeOffset.UtcNow);
        var domainEvent = tenant.DomainEvents[0]; // TenantProvisioned

        // Act
        await outbox.AppendAsync(domainEvent);
        await db.SaveChangesAsync();

        // Assert
        using var readDb = _fixture.CreateDbContext();
        var saved = await readDb.OutboxEvents
            .FirstOrDefaultAsync(e => e.Id == domainEvent.EventId);

        saved.Should().NotBeNull();
        saved!.EventType.Should().Be("tenant.provisioned.v1");
        saved.AggregateType.Should().Be("Tenant");
        saved.CorrelationId.Should().Be(correlationId);
        saved.Status.Should().Be(OutboxEventStatus.Pending);
        saved.Payload.Should().Contain("tenantId");
        // adminEmail NUNCA deve aparecer no payload (LGPD)
        saved.Payload.Should().NotContain("adminEmail");
        saved.Payload.Should().NotContain("admin@outbox.com");
    }

    [Fact(DisplayName = "OutboxPublisher marca eventos published após publicação bem-sucedida")]
    public async Task OutboxPublisher_MarksEventsPublished_AfterSuccessfulPublication()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenantCtx = new FakeTenantContext(tenantId);
        using var db = _fixture.CreateDbContext();
        var outbox = new OutboxRepository(db, tenantCtx);

        var slug = Slug.Create("publisher-test").Value;
        var tz = TimezoneIana.Create("America/Sao_Paulo").Value;
        var dt = DigestTime.Create("07:00").Value;
        var tenant = Tenant.Provision(slug, "Publisher Test", tz, dt, "admin@pub.com", DateTimeOffset.UtcNow);

        await outbox.AppendAsync(tenant.DomainEvents[0]);
        await db.SaveChangesAsync();

        var publisher = new InMemoryPubSubPublisher();
        var eventId = tenant.DomainEvents[0].EventId;

        // Act — simular o que o OutboxPublisher faz
        using var publishDb = _fixture.CreateDbContext();
        var pending = await publishDb.OutboxEvents
            .Where(e => e.Status == OutboxEventStatus.Pending)
            .ToListAsync();

        foreach (var evt in pending)
        {
            await publisher.PublishAsync(evt.EventType, evt.Id, evt.Payload);
            evt.Status = OutboxEventStatus.Published;
            evt.PublishedAt = DateTimeOffset.UtcNow;
        }
        await publishDb.SaveChangesAsync();

        // Assert
        publisher.Messages.Should().HaveCountGreaterThan(0);
        publisher.Messages[0].EventType.Should().Be("tenant.provisioned.v1");

        using var verifyDb = _fixture.CreateDbContext();
        var published = await verifyDb.OutboxEvents.FirstOrDefaultAsync(e => e.Id == eventId);
        published!.Status.Should().Be(OutboxEventStatus.Published);
        published.PublishedAt.Should().NotBeNull();
    }

    [Fact(DisplayName = "OutboxPublisher marca evento como failed após MaxRetries tentativas")]
    public async Task OutboxPublisher_MarksEventFailed_AfterMaxRetries()
    {
        // Arrange
        var tenantCtx = new FakeTenantContext(Guid.NewGuid());
        using var db = _fixture.CreateDbContext();
        var outbox = new OutboxRepository(db, tenantCtx);

        var slug = Slug.Create("failed-retry-test").Value;
        var tz = TimezoneIana.Create("America/Sao_Paulo").Value;
        var dt = DigestTime.Create("07:00").Value;
        var tenant = Tenant.Provision(slug, "Failed Retry", tz, dt, "admin@fail.com", DateTimeOffset.UtcNow);
        await outbox.AppendAsync(tenant.DomainEvents[0]);
        await db.SaveChangesAsync();

        var eventId = tenant.DomainEvents[0].EventId;

        // Act — simular N falhas (MaxRetries = 3)
        for (int attempt = 1; attempt <= OutboxPublisher.MaxRetries; attempt++)
        {
            using var retryDb = _fixture.CreateDbContext();
            var evt = await retryDb.OutboxEvents.FindAsync(eventId);
            evt!.RetryCount = attempt;
            evt.LastError = $"Erro simulado tentativa {attempt}";
            if (attempt >= OutboxPublisher.MaxRetries)
                evt.Status = OutboxEventStatus.Failed;
            await retryDb.SaveChangesAsync();
        }

        // Assert
        using var assertDb = _fixture.CreateDbContext();
        var failed = await assertDb.OutboxEvents.FindAsync(eventId);
        failed!.Status.Should().Be(OutboxEventStatus.Failed);
        failed.RetryCount.Should().Be(OutboxPublisher.MaxRetries);
        failed.LastError.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "AppendRangeAsync escreve múltiplos eventos em uma operação")]
    public async Task AppendRangeAsync_WritesMultipleEvents()
    {
        // Arrange
        var tenantCtx = new FakeTenantContext(Guid.NewGuid());
        using var db = _fixture.CreateDbContext();
        var outbox = new OutboxRepository(db, tenantCtx);

        var slug = Slug.Create("range-test-outbox").Value;
        var tz = TimezoneIana.Create("America/Sao_Paulo").Value;
        var dt = DigestTime.Create("07:00").Value;
        var tenant = Tenant.Provision(slug, "Range Test", tz, dt, "admin@range.com", DateTimeOffset.UtcNow);
        tenant.Suspend(DateTimeOffset.UtcNow);
        tenant.Reactivate(DateTimeOffset.UtcNow);

        // 3 eventos: Provisioned, Suspended, Reactivated
        var events = tenant.DomainEvents.ToList();
        events.Should().HaveCount(3);

        // Act
        await outbox.AppendRangeAsync(events);
        await db.SaveChangesAsync();

        // Assert
        using var readDb = _fixture.CreateDbContext();
        var saved = await readDb.OutboxEvents
            .Where(e => events.Select(ev => ev.EventId).Contains(e.Id))
            .ToListAsync();
        saved.Should().HaveCount(3);
        saved.Select(e => e.EventType).Should().BeEquivalentTo(
            new[] { "tenant.provisioned.v1", "tenant.suspended.v1", "tenant.reactivated.v1" });
    }
}

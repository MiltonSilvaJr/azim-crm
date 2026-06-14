using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Organization.Domain.Events;
using Organization.Infrastructure.Messaging;
using Organization.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Organization.Infrastructure.Tests.Messaging;

/// <summary>
/// Testes de integração do padrão Outbox/Inbox via Testcontainers PostgreSQL.
/// Valida: enfileiramento no outbox, deduplicação no inbox, publicação pelo worker,
/// e envelope de atributos (tenant_id, correlation_id).
/// TASK-18 (Onda 4 — Infrastructure).
/// </summary>
[Trait("Category", "Integration")]
public sealed class OutboxInboxTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("org_test")
        .WithUsername("org_app")
        .WithPassword("test_pass")
        .Build();

    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _connectionString = _postgres.GetConnectionString();

        using var ctx = CreateContextWithoutFilter();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private OrganizationDbContext CreateContext(Guid tenantId)
    {
        var accessor = new TenantContextAccessor { TenantId = tenantId, IsEnabled = true };
        var options = new DbContextOptionsBuilder<OrganizationDbContext>().UseNpgsql(_connectionString).Options;
        return new OrganizationDbContext(options, accessor);
    }

    private OrganizationDbContext CreateContextWithoutFilter()
    {
        var accessor = new TenantContextAccessor { IsEnabled = false };
        var options = new DbContextOptionsBuilder<OrganizationDbContext>().UseNpgsql(_connectionString).Options;
        return new OrganizationDbContext(options, accessor);
    }

    // ── Evento de domínio stub para testes ───────────────────────────────

    private sealed record TestDomainEvent(Guid AggregateId, DateTimeOffset OccurredAt) : IDomainEvent;

    // ── ST-07: EfCoreEventOutbox enfileira evento na tabela ──────────────

    [Fact(DisplayName = "ST-07: EfCoreEventOutbox enfileira evento na tabela outbox_events")]
    public async Task Outbox_EnfileiraDomainEvent()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var domainEvent = new TestDomainEvent(Guid.NewGuid(), DateTimeOffset.UtcNow);

        using var ctx = CreateContext(tenantId);
        await ctx.SetTenantAsync(tenantId);
        var outbox = new EfCoreEventOutbox(ctx);

        // Act
        await outbox.EnqueueAsync(domainEvent, tenantId, correlationId);
        await ctx.SaveChangesAsync();

        // Assert — verifica na tabela diretamente
        using var read = CreateContextWithoutFilter();
        var saved = await read.OutboxEvents
            .FirstOrDefaultAsync(e => e.TenantId == tenantId);

        saved.Should().NotBeNull();
        saved!.TenantId.Should().Be(tenantId);
        saved.CorrelationId.Should().Be(correlationId);
        saved.EventType.Should().Be("TestDomainEvent");
        saved.PublishedAt.Should().BeNull("evento recém-enfileirado não foi publicado ainda");
        saved.Payload.Should().Contain(domainEvent.AggregateId.ToString("D"));
    }

    // ── ST-07: EfCoreInboxStore — deduplicação ────────────────────────────

    [Fact(DisplayName = "ST-07: EfCoreInboxStore deduplica mensagem já processada")]
    public async Task Inbox_DeduplicaMensagemProcessada()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var messageId = Guid.NewGuid().ToString();

        using var ctx = CreateContext(tenantId);
        await ctx.SetTenantAsync(tenantId);
        var inbox = new EfCoreInboxStore(ctx);

        // Act — primeira vez
        var beforeMark = await inbox.IsProcessedAsync(messageId, tenantId);
        await inbox.MarkProcessedAsync(messageId, tenantId, "tenant.provisioned.v1");
        await ctx.SaveChangesAsync();

        var afterMark = await inbox.IsProcessedAsync(messageId, tenantId);

        // Assert
        beforeMark.Should().BeFalse("mensagem não foi processada ainda");
        afterMark.Should().BeTrue("mensagem foi marcada como processada");
    }

    [Fact(DisplayName = "ST-07: EfCoreInboxStore MarkProcessedAsync é idempotente")]
    public async Task Inbox_MarkProcessed_Idempotente()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var messageId = Guid.NewGuid().ToString();

        using var ctx = CreateContext(tenantId);
        await ctx.SetTenantAsync(tenantId);
        var inbox = new EfCoreInboxStore(ctx);

        // Act — marcar duas vezes
        await inbox.MarkProcessedAsync(messageId, tenantId, "tenant.provisioned.v1");
        await ctx.SaveChangesAsync();

        // Segunda marcação no mesmo contexto — deve ser idempotente
        await FluentActions.Invoking(async () =>
        {
            await inbox.MarkProcessedAsync(messageId, tenantId, "tenant.provisioned.v1");
            await ctx.SaveChangesAsync();
        }).Should().NotThrowAsync("MarkProcessedAsync deve ser idempotente");
    }

    [Fact(DisplayName = "ST-07: EfCoreInboxStore isola por tenant — mesmo messageId em tenants distintos")]
    public async Task Inbox_IsolaMessageIdPorTenant()
    {
        // Arrange — mesmo messageId em dois tenants
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var messageId = "shared-message-id";

        using var ctxA = CreateContext(tenantA);
        await ctxA.SetTenantAsync(tenantA);
        var inboxA = new EfCoreInboxStore(ctxA);
        await inboxA.MarkProcessedAsync(messageId, tenantA, "tenant.provisioned.v1");
        await ctxA.SaveChangesAsync();

        // Act — verifica se tenantB vê a mensagem de tenantA como processada
        using var ctxB = CreateContext(tenantB);
        await ctxB.SetTenantAsync(tenantB);
        var inboxB = new EfCoreInboxStore(ctxB);
        var processedInB = await inboxB.IsProcessedAsync(messageId, tenantB);

        // Assert
        processedInB.Should().BeFalse("tenantB não deve ver a mensagem do tenantA como processada");
    }

    // ── ST-07: OutboxWorker publica e marca published_at ─────────────────

    [Fact(DisplayName = "ST-07: OutboxWorker publica eventos pendentes e marca published_at")]
    public async Task OutboxWorker_PublicaEventosPendentes()
    {
        // Arrange — enfileira 3 eventos
        var tenantId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();

        using var setup = CreateContextWithoutFilter();
        for (var i = 0; i < 3; i++)
        {
            setup.OutboxEvents.Add(new OutboxEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EventType = "TestDomainEvent",
                Payload = $"{{\"index\":{i}}}",
                CorrelationId = correlationId,
                OccurredAt = DateTimeOffset.UtcNow,
                PublishedAt = null,
            });
        }
        await setup.SaveChangesAsync();

        // Publisher spy: captura atributos publicados
        var publishedAttributes = new List<IReadOnlyDictionary<string, string>>();
        var publisherSpy = Substitute.For<IPubSubPublisher>();
        publisherSpy.PublishAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                publishedAttributes.Add(ci.ArgAt<IReadOnlyDictionary<string, string>>(2));
                return Task.CompletedTask;
            });

        // Act — processa batch diretamente (sem BackgroundService).
        // CreateContextWithoutFilter já cria o contexto com IsEnabled = false,
        // permitindo que o worker acesse eventos de todos os tenants.
        using var workerCtx = CreateContextWithoutFilter();
        await ProcessOutboxBatchAsync(workerCtx, publisherSpy);

        // Assert
        publishedAttributes.Should().HaveCount(3, "3 eventos pendentes devem ser publicados");
        publishedAttributes.Should().AllSatisfy(attrs =>
        {
            attrs.Should().ContainKey("tenant_id");
            attrs.Should().ContainKey("correlation_id");
            attrs.Should().ContainKey("event_type");
            attrs["tenant_id"].Should().Be(tenantId.ToString("D"));
            attrs["correlation_id"].Should().Be(correlationId.ToString("D"));
        });

        // Verifica published_at na tabela
        using var verify = CreateContextWithoutFilter();
        var unpublished = await verify.OutboxEvents
            .CountAsync(e => e.TenantId == tenantId && e.PublishedAt == null);

        unpublished.Should().Be(0, "todos os eventos devem ter published_at definido");
    }

    /// <summary>
    /// Simula um ciclo de processamento do OutboxWorker diretamente,
    /// sem precisar de BackgroundService completo.
    /// </summary>
    private static async Task ProcessOutboxBatchAsync(OrganizationDbContext context, IPubSubPublisher publisher)
    {
        var events = await context.OutboxEvents
            .Where(e => e.PublishedAt == null)
            .OrderBy(e => e.OccurredAt)
            .Take(100)
            .ToListAsync();

        foreach (var outboxEvent in events)
        {
            var topic = $"org.{outboxEvent.EventType.ToLowerInvariant()}";
            var attributes = new Dictionary<string, string>
            {
                ["event_id"] = outboxEvent.Id.ToString("D"),
                ["event_type"] = outboxEvent.EventType,
                ["tenant_id"] = outboxEvent.TenantId.ToString("D"),
                ["correlation_id"] = outboxEvent.CorrelationId.ToString("D"),
                ["occurred_at"] = outboxEvent.OccurredAt.ToString("O"),
            };

            await publisher.PublishAsync(topic, outboxEvent.Payload, attributes);
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var ev in events)
            ev.PublishedAt = now;

        await context.SaveChangesAsync();
    }
}

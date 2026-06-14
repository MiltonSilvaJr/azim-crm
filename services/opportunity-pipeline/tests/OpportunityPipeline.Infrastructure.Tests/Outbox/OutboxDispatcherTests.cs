using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Domain.Opportunities.Events;
using OpportunityPipeline.Infrastructure.Outbox;
using OpportunityPipeline.Infrastructure.Persistence;

namespace OpportunityPipeline.Infrastructure.Tests.Outbox;

/// <summary>
/// Testes do OutboxDomainEventDispatcher — verifica que eventos de domínio
/// são persistidos no outbox_events na mesma transação (ADR-0004, Req 20.4).
/// Mapeia: TASK-16, ADR-0004.
/// </summary>
[Collection("PostgresCollection")]
public sealed class OutboxDispatcherTests(PostgresFixture fixture)
{
    // =========================================================================
    // Teste 1: evento persiste no outbox com status "pending"
    // =========================================================================

    [Fact(DisplayName = "OUTBOX_01: DispatchAsync deve persistir evento no outbox com status pending")]
    public async Task DispatchAsync_ShouldPersistEventAsPending()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var ctx = await fixture.CreateContextAsync(tenantId);

        var dispatcher = new OutboxDomainEventDispatcher(
            ctx,
            NullLogger<OutboxDomainEventDispatcher>.Instance);

        var domainEvent = new OpportunityCreated(
            EventId: Guid.NewGuid(),
            TenantId: tenantId,
            OpportunityId: Guid.NewGuid(),
            OpportunityNumber: "AZ-0001",
            BuId: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            OwnerId: Guid.NewGuid(),
            StageId: Guid.NewGuid(),
            OriginChannelId: Guid.NewGuid(),
            ActorId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow);

        // Act
        await dispatcher.DispatchAsync(domainEvent);
        await ctx.SaveChangesAsync();

        // Assert
        // Reseta contexto para ler sem cache
        ctx.ChangeTracker.Clear();
        var message = await ctx.OutboxMessages
            .FirstOrDefaultAsync(m => m.Id == domainEvent.EventId);

        message.Should().NotBeNull("o evento deve estar no outbox.");
        message!.Status.Should().Be("pending",
            "eventos recém-enfileirados devem ter status 'pending'.");
        message.EventType.Should().Be("opportunity.created.v1",
            "o tipo do evento deve ser mapeado corretamente.");
        message.TenantId.Should().Be(tenantId,
            "o tenant_id deve estar correto para RLS.");
    }

    // =========================================================================
    // Teste 2: DispatchAllAsync persiste múltiplos eventos
    // =========================================================================

    [Fact(DisplayName = "OUTBOX_02: DispatchAllAsync deve persistir múltiplos eventos")]
    public async Task DispatchAllAsync_ShouldPersistAllEvents()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var ctx = await fixture.CreateContextAsync(tenantId);

        var dispatcher = new OutboxDomainEventDispatcher(
            ctx,
            NullLogger<OutboxDomainEventDispatcher>.Instance);

        var oppId = Guid.NewGuid();
        var events = new DomainEvent[]
        {
            new OpportunityWon(
                EventId: Guid.NewGuid(),
                TenantId: tenantId,
                OpportunityId: oppId,
                ValorTotalCents: 100000L,
                ClosedAt: DateTimeOffset.UtcNow,
                ActorId: Guid.NewGuid(),
                OccurredAt: DateTimeOffset.UtcNow),

            new CommissionSnapshotCreated(
                EventId: Guid.NewGuid(),
                TenantId: tenantId,
                OpportunityId: oppId,
                PartnerId: Guid.NewGuid(),
                CommissionId: Guid.NewGuid(),
                ComissaoTotalCents: 10000L,
                SnapshotAt: DateTimeOffset.UtcNow,
                OccurredAt: DateTimeOffset.UtcNow)
        };

        // Act
        await dispatcher.DispatchAllAsync(events);
        await ctx.SaveChangesAsync();

        // Assert
        ctx.ChangeTracker.Clear();
        var count = await ctx.OutboxMessages
            .CountAsync(m => m.TenantId == tenantId && m.Status == "pending");

        count.Should().Be(2,
            "ambos os eventos devem estar no outbox com status pending (Req 20.4).");
    }

    // =========================================================================
    // Teste 3: mapeamento correto de tipos de evento
    // =========================================================================

    [Theory(DisplayName = "OUTBOX_03: tipos de evento devem ser mapeados para nomes versionados .v1")]
    [InlineData("opportunity.won.v1")]
    [InlineData("opportunity.created.v1")]
    public async Task EventTypes_ShouldBeMappedToVersionedNames(string expectedEventType)
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var ctx = await fixture.CreateContextAsync(tenantId);

        var dispatcher = new OutboxDomainEventDispatcher(
            ctx,
            NullLogger<OutboxDomainEventDispatcher>.Instance);

        DomainEvent domainEvent = expectedEventType switch
        {
            "opportunity.won.v1" => new OpportunityWon(
                Guid.NewGuid(), tenantId, Guid.NewGuid(), 100000L,
                DateTimeOffset.UtcNow, Guid.NewGuid(), DateTimeOffset.UtcNow),
            "opportunity.created.v1" => new OpportunityCreated(
                Guid.NewGuid(), tenantId, Guid.NewGuid(), "AZ-0002",
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow),
            _ => throw new InvalidOperationException()
        };

        // Act
        await dispatcher.DispatchAsync(domainEvent);
        await ctx.SaveChangesAsync();

        ctx.ChangeTracker.Clear();
        var message = await ctx.OutboxMessages.FirstOrDefaultAsync(m => m.Id == domainEvent.EventId);

        // Assert
        message!.EventType.Should().Be(expectedEventType,
            $"o evento deve ser mapeado para '{expectedEventType}'.");
    }
}

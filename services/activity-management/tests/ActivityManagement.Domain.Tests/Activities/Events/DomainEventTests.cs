namespace ActivityManagement.Domain.Tests.Activities.Events;

using ActivityManagement.Domain.Activities.Events;

/// <summary>
/// Testes para os eventos de domínio ActivityCreated, ActivityCompleted, ActivityOverdue.
/// Critérios: imutabilidade, campos obrigatórios presentes, ausência de title/description (RNF 7.2).
/// Mapeia: design §4.4, Req 14, TASK-03 ST-01.
/// </summary>
public sealed class DomainEventTests
{
    private static readonly Guid ActivityId  = Guid.NewGuid();
    private static readonly Guid TenantId    = Guid.NewGuid();
    private static readonly Guid BuId        = Guid.NewGuid();
    private static readonly Guid OwnerId     = Guid.NewGuid();
    private static readonly Guid OpportId    = Guid.NewGuid();
    private static readonly Guid AccountId   = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    // ── ActivityCreated ──────────────────────────────────────────────────────

    [Fact]
    public void ActivityCreated_CarriesRequiredFields()
    {
        var evt = new ActivityCreated(
            EventId:       Guid.NewGuid(),
            OccurredAt:    Now,
            ActivityId:    ActivityId,
            TenantId:      TenantId,
            BuId:          BuId,
            OwnerId:       OwnerId,
            Type:          "meeting",
            DueAt:         Now.AddDays(1),
            CorrelationId: Guid.NewGuid(),
            OpportunityId: OpportId,
            AccountId:     AccountId);

        evt.ActivityId.Should().Be(ActivityId);
        evt.TenantId.Should().Be(TenantId);
        evt.BuId.Should().Be(BuId);
        evt.OwnerId.Should().Be(OwnerId);
        evt.Type.Should().Be("meeting");
        evt.DueAt.Should().Be(Now.AddDays(1));
        evt.OpportunityId.Should().Be(OpportId);
        evt.AccountId.Should().Be(AccountId);
        evt.EventId.Should().NotBeEmpty();
        evt.OccurredAt.Should().Be(Now);
    }

    [Fact]
    public void ActivityCreated_DoesNotExposeTitle()
    {
        // Verifica que o record NÃO possui propriedade Title nem Description (RNF 7.2)
        var type = typeof(ActivityCreated);
        type.GetProperty("Title").Should().BeNull("título não deve constar no evento (RNF 7.2)");
        type.GetProperty("Description").Should().BeNull("descrição não deve constar no evento (RNF 7.2)");
    }

    [Fact]
    public void ActivityCreated_OptionalFields_AcceptNull()
    {
        var evt = new ActivityCreated(
            EventId:       Guid.NewGuid(),
            OccurredAt:    Now,
            ActivityId:    ActivityId,
            TenantId:      TenantId,
            BuId:          BuId,
            OwnerId:       OwnerId,
            Type:          "call",
            DueAt:         Now,
            CorrelationId: Guid.NewGuid(),
            OpportunityId: null,
            AccountId:     null);

        evt.OpportunityId.Should().BeNull();
        evt.AccountId.Should().BeNull();
    }

    // ── ActivityCompleted ────────────────────────────────────────────────────

    [Fact]
    public void ActivityCompleted_CarriesRequiredFields()
    {
        var completedAt = Now;
        var evt = new ActivityCompleted(
            EventId:       Guid.NewGuid(),
            OccurredAt:    Now,
            ActivityId:    ActivityId,
            TenantId:      TenantId,
            OwnerId:       OwnerId,
            CompletedAt:   completedAt,
            CorrelationId: Guid.NewGuid(),
            OpportunityId: OpportId);

        evt.ActivityId.Should().Be(ActivityId);
        evt.TenantId.Should().Be(TenantId);
        evt.OwnerId.Should().Be(OwnerId);
        evt.CompletedAt.Should().Be(completedAt);
        evt.OpportunityId.Should().Be(OpportId);
    }

    [Fact]
    public void ActivityCompleted_DoesNotExposeTitle()
    {
        var type = typeof(ActivityCompleted);
        type.GetProperty("Title").Should().BeNull("título não deve constar no evento (RNF 7.2)");
        type.GetProperty("Description").Should().BeNull("descrição não deve constar no evento (RNF 7.2)");
    }

    // ── ActivityOverdue ──────────────────────────────────────────────────────

    [Fact]
    public void ActivityOverdue_CarriesRequiredFields()
    {
        var dueAt    = Now.AddDays(-1);
        var scanDate = DateOnly.FromDateTime(Now.UtcDateTime);

        var evt = new ActivityOverdue(
            EventId:       Guid.NewGuid(),
            OccurredAt:    Now,
            ActivityId:    ActivityId,
            TenantId:      TenantId,
            OwnerId:       OwnerId,
            DueAt:         dueAt,
            ScanDate:      scanDate,
            CorrelationId: Guid.NewGuid(),
            OpportunityId: OpportId);

        evt.ActivityId.Should().Be(ActivityId);
        evt.TenantId.Should().Be(TenantId);
        evt.OwnerId.Should().Be(OwnerId);
        evt.DueAt.Should().Be(dueAt);
        evt.ScanDate.Should().Be(scanDate);
        evt.OpportunityId.Should().Be(OpportId);
    }

    [Fact]
    public void ActivityOverdue_DoesNotExposeTitle()
    {
        var type = typeof(ActivityOverdue);
        type.GetProperty("Title").Should().BeNull("título não deve constar no evento (RNF 7.2)");
        type.GetProperty("Description").Should().BeNull("descrição não deve constar no evento (RNF 7.2)");
    }

    // ── Base DomainEvent ─────────────────────────────────────────────────────

    [Fact]
    public void AllEventTypes_InheritFromDomainEvent()
    {
        typeof(ActivityCreated).IsAssignableTo(typeof(DomainEvent)).Should().BeTrue();
        typeof(ActivityCompleted).IsAssignableTo(typeof(DomainEvent)).Should().BeTrue();
        typeof(ActivityOverdue).IsAssignableTo(typeof(DomainEvent)).Should().BeTrue();
    }

    [Fact]
    public void DomainEvent_EventId_IsNotEmpty()
    {
        var id = Guid.NewGuid();
        var evt = new ActivityCompleted(
            EventId:       id,
            OccurredAt:    Now,
            ActivityId:    ActivityId,
            TenantId:      TenantId,
            OwnerId:       OwnerId,
            CompletedAt:   Now,
            CorrelationId: Guid.NewGuid(),
            OpportunityId: null);

        evt.EventId.Should().Be(id);
        evt.OccurredAt.Should().Be(Now);
    }
}

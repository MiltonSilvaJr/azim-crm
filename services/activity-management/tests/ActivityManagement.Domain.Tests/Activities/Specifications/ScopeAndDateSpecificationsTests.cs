namespace ActivityManagement.Domain.Tests.Activities.Specifications;

using ActivityManagement.Domain.Activities;
using ActivityManagement.Domain.Activities.Specifications;
using ActivityManagement.Domain.Activities.ValueObjects;

/// <summary>
/// Testes para TodaySpecification, UpcomingSpecification e ScopeSpecification.
/// Mapeia: design §4.6, Req 5, Req 13, TASK-05.
/// </summary>
public sealed class ScopeAndDateSpecificationsTests
{
    private static readonly Guid TenantId  = Guid.NewGuid();
    private static readonly Guid TenantId2 = Guid.NewGuid();
    private static readonly Guid BuId      = Guid.NewGuid();
    private static readonly Guid OwnerId   = Guid.NewGuid();
    private static readonly Guid OwnerId2  = Guid.NewGuid();

    // Referência: 2026-06-14 12:00 UTC, fuso America/Sao_Paulo (UTC-3) → dia 14 local
    private static readonly DateTimeOffset Now =
        new(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeZoneInfo TenantTz =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private static Activity CreateActivity(
        Guid ownerId, Guid tenantId, string status, DateTimeOffset dueAt)
    {
        var activity = Activity.Create(
            tenantId:      tenantId,
            buId:          BuId,
            ownerId:       ownerId,
            type:          ActivityType.Create("meeting"),
            title:         "Teste",
            dueAt:         DueDate.Create(dueAt),
            now:           Now.AddDays(-1),
            correlationId: Guid.NewGuid());

        switch (status)
        {
            case "in_progress": activity.ChangeStatus(ActivityStatus.InProgress, Now.AddDays(-1)); break;
            case "completed":   activity.Complete(Now.AddDays(-1)); break;
            case "cancelled":   activity.Cancel(Now.AddDays(-1)); break;
        }

        return activity;
    }

    // ── TodaySpecification ───────────────────────────────────────────────────

    [Fact]
    public void TodaySpec_ActivityDueToday_LocalTz_IsToday()
    {
        // 2026-06-14 09:00 UTC = 06:00 America/Sao_Paulo → mesmo dia local
        var dueAt    = new DateTimeOffset(2026, 6, 14, 9, 0, 0, TimeSpan.Zero);
        var spec     = new TodaySpecification(Now, TenantTz);
        var activity = CreateActivity(OwnerId, TenantId, "pending", dueAt);
        spec.IsSatisfiedBy(activity).Should().BeTrue();
    }

    [Fact]
    public void TodaySpec_ActivityDueTomorrow_IsNotToday()
    {
        var dueAt    = new DateTimeOffset(2026, 6, 15, 9, 0, 0, TimeSpan.Zero);
        var spec     = new TodaySpecification(Now, TenantTz);
        var activity = CreateActivity(OwnerId, TenantId, "pending", dueAt);
        spec.IsSatisfiedBy(activity).Should().BeFalse();
    }

    [Fact]
    public void TodaySpec_Terminal_IsNotToday()
    {
        var dueAt    = new DateTimeOffset(2026, 6, 14, 9, 0, 0, TimeSpan.Zero);
        var spec     = new TodaySpecification(Now, TenantTz);
        var activity = CreateActivity(OwnerId, TenantId, "completed", dueAt);
        spec.IsSatisfiedBy(activity).Should().BeFalse("terminal nunca aparece nas faixas");
    }

    // ── UpcomingSpecification ────────────────────────────────────────────────

    [Fact]
    public void UpcomingSpec_ActivityDueTomorrow_IsUpcoming()
    {
        var dueAt    = new DateTimeOffset(2026, 6, 15, 9, 0, 0, TimeSpan.Zero);
        var spec     = new UpcomingSpecification(Now, TenantTz);
        var activity = CreateActivity(OwnerId, TenantId, "pending", dueAt);
        spec.IsSatisfiedBy(activity).Should().BeTrue();
    }

    [Fact]
    public void UpcomingSpec_ActivityDueToday_IsNotUpcoming()
    {
        var dueAt    = new DateTimeOffset(2026, 6, 14, 9, 0, 0, TimeSpan.Zero);
        var spec     = new UpcomingSpecification(Now, TenantTz);
        var activity = CreateActivity(OwnerId, TenantId, "pending", dueAt);
        spec.IsSatisfiedBy(activity).Should().BeFalse("hoje não é 'próximo', é hoje");
    }

    [Fact]
    public void UpcomingSpec_Terminal_IsNotUpcoming()
    {
        var dueAt    = new DateTimeOffset(2026, 6, 15, 9, 0, 0, TimeSpan.Zero);
        var spec     = new UpcomingSpecification(Now, TenantTz);
        var activity = CreateActivity(OwnerId, TenantId, "cancelled", dueAt);
        spec.IsSatisfiedBy(activity).Should().BeFalse();
    }

    // ── ScopeSpecification (Vendedor) ────────────────────────────────────────

    [Fact]
    public void ScopeSpec_SellerSeesOwnActivities()
    {
        var spec     = ScopeSpecification.ForSeller(TenantId, OwnerId);
        var activity = CreateActivity(OwnerId, TenantId, "pending",
            DateTimeOffset.UtcNow.AddDays(1));
        spec.IsSatisfiedBy(activity).Should().BeTrue();
    }

    [Fact]
    public void ScopeSpec_SellerDoesNotSeeOtherOwner()
    {
        var spec     = ScopeSpecification.ForSeller(TenantId, OwnerId);
        var activity = CreateActivity(OwnerId2, TenantId, "pending",
            DateTimeOffset.UtcNow.AddDays(1));
        spec.IsSatisfiedBy(activity).Should().BeFalse();
    }

    [Fact]
    public void ScopeSpec_SellerDoesNotSeeDifferentTenant()
    {
        var spec     = ScopeSpecification.ForSeller(TenantId, OwnerId);
        var activity = CreateActivity(OwnerId, TenantId2, "pending",
            DateTimeOffset.UtcNow.AddDays(1));
        spec.IsSatisfiedBy(activity).Should().BeFalse(
            "ScopeSpecification não deve retornar atividades de outro tenant");
    }

    // ── ScopeSpecification (Gestor de BU) ───────────────────────────────────

    [Fact]
    public void ScopeSpec_ManagerSeesAllOwnersInBu()
    {
        var spec     = ScopeSpecification.ForManager(TenantId, BuId);
        var activity = CreateActivity(OwnerId2, TenantId, "pending",
            DateTimeOffset.UtcNow.AddDays(1));
        spec.IsSatisfiedBy(activity).Should().BeTrue();
    }

    [Fact]
    public void ScopeSpec_ManagerDoesNotSeeDifferentTenant()
    {
        var spec     = ScopeSpecification.ForManager(TenantId, BuId);
        var activity = CreateActivity(OwnerId, TenantId2, "pending",
            DateTimeOffset.UtcNow.AddDays(1));
        spec.IsSatisfiedBy(activity).Should().BeFalse();
    }
}

namespace ActivityManagement.Domain.Tests.Activities.Specifications;

using ActivityManagement.Domain.Activities;
using ActivityManagement.Domain.Activities.Specifications;
using ActivityManagement.Domain.Activities.ValueObjects;

/// <summary>
/// Testes unitários e PBT-05 para <see cref="FunnelHealthSpecification"/>.
/// Propriedade: oportunidade tem follow-up futuro ⟺ existe ≥1 atividade não terminal
/// com dueAt no futuro vinculada a ela.
/// Mapeia: design §4.6, Req 10.1, PBT-05, TASK-05.
/// </summary>
public sealed class FunnelHealthSpecificationTests
{
    private static readonly Guid TenantId      = Guid.NewGuid();
    private static readonly Guid BuId          = Guid.NewGuid();
    private static readonly Guid OwnerId       = Guid.NewGuid();
    private static readonly Guid OpportunityId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);

    private static Activity CreateLinkedActivity(
        Guid oppId, string status, DateTimeOffset dueAt)
    {
        var activity = Activity.Create(
            tenantId:        TenantId,
            buId:            BuId,
            ownerId:         OwnerId,
            type:            ActivityType.Create("follow_up"),
            title:           "Follow-up",
            dueAt:           DueDate.Create(dueAt),
            now:             Now.AddDays(-1),
            opportunityLink: OpportunityLink.Create(oppId),
            correlationId:   Guid.NewGuid());

        switch (status)
        {
            case "in_progress": activity.ChangeStatus(ActivityStatus.InProgress, Now.AddDays(-1)); break;
            case "completed":   activity.Complete(Now.AddDays(-1)); break;
            case "cancelled":   activity.Cancel(Now.AddDays(-1)); break;
        }

        return activity;
    }

    // ── Casos determinísticos ────────────────────────────────────────────────

    [Fact]
    public void WithFuturePendingActivity_HasFollowup()
    {
        var activities = new List<Activity>
        {
            CreateLinkedActivity(OpportunityId, "pending", Now.AddDays(1))
        };
        var spec = new FunnelHealthSpecification(Now);
        spec.HasFollowup(OpportunityId, activities).Should().BeTrue();
    }

    [Fact]
    public void WithFutureInProgressActivity_HasFollowup()
    {
        var activities = new List<Activity>
        {
            CreateLinkedActivity(OpportunityId, "in_progress", Now.AddDays(1))
        };
        var spec = new FunnelHealthSpecification(Now);
        spec.HasFollowup(OpportunityId, activities).Should().BeTrue();
    }

    [Fact]
    public void WithOnlyCompletedActivity_NoFollowup()
    {
        var activities = new List<Activity>
        {
            CreateLinkedActivity(OpportunityId, "completed", Now.AddDays(1))
        };
        var spec = new FunnelHealthSpecification(Now);
        spec.HasFollowup(OpportunityId, activities).Should().BeFalse(
            "atividade completed não conta como follow-up");
    }

    [Fact]
    public void WithOnlyPastPendingActivity_NoFollowup()
    {
        var activities = new List<Activity>
        {
            CreateLinkedActivity(OpportunityId, "pending", Now.AddDays(-1))
        };
        var spec = new FunnelHealthSpecification(Now);
        spec.HasFollowup(OpportunityId, activities).Should().BeFalse(
            "atividade com dueAt no passado não é follow-up futuro");
    }

    [Fact]
    public void EmptyList_NoFollowup()
    {
        var spec = new FunnelHealthSpecification(Now);
        spec.HasFollowup(OpportunityId, []).Should().BeFalse();
    }

    [Fact]
    public void MixedActivities_OneValidFuture_HasFollowup()
    {
        var oppId2 = Guid.NewGuid();
        var activities = new List<Activity>
        {
            CreateLinkedActivity(OpportunityId, "completed",   Now.AddDays(1)),  // terminal
            CreateLinkedActivity(OpportunityId, "pending",     Now.AddDays(-1)), // passado
            CreateLinkedActivity(OpportunityId, "in_progress", Now.AddDays(2)),  // válida
        };
        var spec = new FunnelHealthSpecification(Now);
        spec.HasFollowup(OpportunityId, activities).Should().BeTrue();
    }
}

/// <summary>
/// PBT-05 — Saúde do funil após sugestão aceita.
/// Após aceitar sugestão que cria ≥1 atividade não terminal com dueAt futuro,
/// FunnelHealthSpecification retorna true para a oportunidade.
/// Mapeia: PBT-05, design §13, TASK-05.
/// </summary>
public sealed class FunnelHealthSpecificationPbt05Tests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BuId     = Guid.NewGuid();
    private static readonly Guid OwnerId  = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);

    private static Activity CreateFutureFollowup(Guid oppId, int daysAhead)
    {
        return Activity.Create(
            tenantId:        TenantId,
            buId:            BuId,
            ownerId:         OwnerId,
            type:            ActivityType.Create("follow_up"),
            title:           "Follow-up sugerido",
            dueAt:           DueDate.Create(Now.AddDays(daysAhead)),
            now:             Now,
            opportunityLink: OpportunityLink.Create(oppId),
            correlationId:   Guid.NewGuid());
    }

    /// <summary>
    /// PBT-05a: após adicionar N≥1 atividades não terminais com dueAt futuro,
    /// HasFollowup retorna true — independente de quantas atividades passadas/terminais existem.
    /// </summary>
    [Property(MaxTest = 200)]
    public Property AfterAddingFutureFollowup_HasFollowupIsTrue(PositiveInt count, PositiveInt days)
    {
        var oppId      = Guid.NewGuid();
        var futureCount = count.Get % 5 + 1; // 1..5 atividades futuras
        var daysAhead   = days.Get % 365 + 1;

        var activities = Enumerable.Range(0, futureCount)
            .Select(_ => CreateFutureFollowup(oppId, daysAhead))
            .ToList();

        var spec = new FunnelHealthSpecification(Now);
        return spec.HasFollowup(oppId, activities).ToProperty();
    }

    /// <summary>
    /// PBT-05b: lista composta apenas por terminais e/ou passadas → HasFollowup retorna false.
    /// </summary>
    [Property(MaxTest = 200)]
    public Property WithOnlyPastOrTerminalActivities_NoFollowup(PositiveInt count)
    {
        var oppId = Guid.NewGuid();
        // Cria atividades que estão no passado (vencidas) e as conclui — terminais com dueAt passado
        var activities = Enumerable.Range(1, count.Get % 5 + 1)
            .Select(i =>
            {
                var a = Activity.Create(
                    tenantId:        TenantId,
                    buId:            BuId,
                    ownerId:         OwnerId,
                    type:            ActivityType.Create("task"),
                    title:           "Antiga",
                    dueAt:           DueDate.Create(Now.AddDays(-i)),
                    now:             Now.AddDays(-(i + 1)),
                    opportunityLink: OpportunityLink.Create(oppId),
                    correlationId:   Guid.NewGuid());
                a.Complete(Now.AddDays(-(i + 1)));
                return a;
            })
            .ToList();

        var spec = new FunnelHealthSpecification(Now);
        return (!spec.HasFollowup(oppId, activities)).ToProperty();
    }
}

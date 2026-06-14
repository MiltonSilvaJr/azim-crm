namespace ActivityManagement.Domain.Tests.Activities;

using ActivityManagement.Domain.Activities;
using ActivityManagement.Domain.Activities.Events;
using ActivityManagement.Domain.Activities.Exceptions;
using ActivityManagement.Domain.Activities.ValueObjects;

/// <summary>
/// Testes unitários do Aggregate Root <see cref="Activity"/>.
/// Cobre: factory Create/Reconstitute, invariantes I1–I6, métodos de comportamento
/// e idempotência de Complete.
/// Mapeia: design §4.1, Req 1, Req 2, Req 4, Req 6, TASK-04.
/// </summary>
public sealed class ActivityAggregateTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BuId     = Guid.NewGuid();
    private static readonly Guid OwnerId  = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static Activity CreateValid(
        string title = "Reunião inicial",
        string type  = "meeting",
        DateTimeOffset? dueAt = null,
        Priority? priority = null,
        OpportunityLink? oppLink = null,
        AccountLink? accLink = null,
        string? description = null)
    {
        return Activity.Create(
            tenantId:        TenantId,
            buId:            BuId,
            ownerId:         OwnerId,
            type:            ActivityType.Create(type),
            title:           title,
            dueAt:           DueDate.Create(dueAt ?? Now.AddDays(1)),
            now:             Now,
            priority:        priority,
            description:     description,
            opportunityLink: oppLink,
            accountLink:     accLink,
            correlationId:   Guid.NewGuid());
    }

    // ── I1: título não vazio ─────────────────────────────────────────────────

    [Fact]
    public void Create_EmptyTitle_ThrowsTitleRequiredException()
    {
        var act = () => CreateValid(title: "");
        act.Should().Throw<TitleRequiredException>();
    }

    [Fact]
    public void Create_WhitespaceTitle_ThrowsTitleRequiredException()
    {
        var act = () => CreateValid(title: "   ");
        act.Should().Throw<TitleRequiredException>();
    }

    [Fact]
    public void Create_ValidTitle_Succeeds()
    {
        var activity = CreateValid(title: "Ligação de follow-up");
        activity.Title.Should().Be("Ligação de follow-up");
    }

    // ── I2: tipo válido ──────────────────────────────────────────────────────

    [Fact]
    public void Create_InvalidType_ThrowsInvalidActivityTypeException()
    {
        var act = () => Activity.Create(
            tenantId:      TenantId,
            buId:          BuId,
            ownerId:       OwnerId,
            type:          ActivityType.Create("meeting"), // tipo válido — mas queremos testar com inválido
            title:         "Teste",
            dueAt:         DueDate.Create(Now.AddDays(1)),
            now:           Now,
            priority:      null,
            description:   null,
            opportunityLink: null,
            accountLink:   null,
            correlationId: Guid.NewGuid());

        // Para testar tipo inválido, testamos que ActivityType.Create lança
        var actType = () => ActivityType.Create("invalid_type");
        actType.Should().Throw<InvalidActivityTypeException>();
    }

    // ── I3: defaults status=pending, priority=medium ─────────────────────────

    [Fact]
    public void Create_WithoutPriority_DefaultsMedium()
    {
        var activity = CreateValid();
        activity.Priority.Value.Should().Be("medium");
    }

    [Fact]
    public void Create_WithPriority_UsesProvided()
    {
        var activity = CreateValid(priority: Priority.Create("high"));
        activity.Priority.Value.Should().Be("high");
    }

    [Fact]
    public void Create_StatusIsPending()
    {
        var activity = CreateValid();
        activity.Status.Value.Should().Be("pending");
    }

    [Fact]
    public void Create_AccumulatesActivityCreatedEvent()
    {
        var activity = CreateValid();
        activity.DomainEvents.Should().ContainSingle(e => e is ActivityCreated);
    }

    // ── I4: transições de status via ChangeStatus ────────────────────────────

    [Fact]
    public void ChangeStatus_ValidTransition_UpdatesStatus()
    {
        var activity = CreateValid();
        activity.ChangeStatus(ActivityStatus.InProgress, Now);
        activity.Status.Value.Should().Be("in_progress");
    }

    [Fact]
    public void ChangeStatus_InvalidTransition_ThrowsInvalidStatusTransitionException()
    {
        // pending → in_progress (OK), então in_progress → pending (OK),
        // depois pending → in_progress (OK), mas pending → pending (inválido — same→same)
        // A máquina rejeita self-transition (pending → pending), mas o guard de terminal não atua.
        // Testamos uma transição realmente inválida sem ser terminal:
        // in_progress → in_progress é inválida (CanTransitionTo retorna false para mesma origem)
        var activity = CreateValid(); // pending
        activity.ChangeStatus(ActivityStatus.InProgress, Now); // pending → in_progress (OK)
        // in_progress → in_progress (inválida — não está na lista de transições)
        var act = () => activity.ChangeStatus(ActivityStatus.InProgress, Now);
        act.Should().Throw<InvalidStatusTransitionException>();
    }

    // ── I5: completedAt preenchido sse status=completed ──────────────────────

    [Fact]
    public void Complete_SetsCompletedAt()
    {
        var activity = CreateValid();
        activity.Complete(Now);
        activity.CompletedAt.Should().Be(Now);
        activity.Status.Value.Should().Be("completed");
    }

    [Fact]
    public void Cancel_DoesNotSetCompletedAt()
    {
        var activity = CreateValid();
        activity.Cancel(Now);
        activity.CompletedAt.Should().BeNull();
        activity.Status.Value.Should().Be("cancelled");
    }

    [Fact]
    public void InProgress_CompletedAtIsNull()
    {
        var activity = CreateValid();
        activity.ChangeStatus(ActivityStatus.InProgress, Now);
        activity.CompletedAt.Should().BeNull();
    }

    // ── I6: terminal rejeita escrita ─────────────────────────────────────────

    [Fact]
    public void Complete_OnCancelledActivity_ThrowsActivityTerminalException()
    {
        var activity = CreateValid();
        activity.Cancel(Now);
        var act = () => activity.Complete(Now);
        act.Should().Throw<ActivityTerminalException>();
    }

    [Fact]
    public void Cancel_OnCompletedActivity_ThrowsActivityTerminalException()
    {
        var activity = CreateValid();
        activity.Complete(Now);
        var act = () => activity.Cancel(Now);
        act.Should().Throw<ActivityTerminalException>();
    }

    [Fact]
    public void UpdateDetails_OnTerminal_ThrowsActivityTerminalException()
    {
        var activity = CreateValid();
        activity.Complete(Now);
        var act = () => activity.UpdateDetails("Novo título", null, DueDate.Create(Now.AddDays(2)), null, Now);
        act.Should().Throw<ActivityTerminalException>();
    }

    [Fact]
    public void Reschedule_OnTerminal_ThrowsActivityTerminalException()
    {
        var activity = CreateValid();
        activity.Complete(Now);
        var act = () => activity.Reschedule(DueDate.Create(Now.AddDays(2)), Now);
        act.Should().Throw<ActivityTerminalException>();
    }

    [Fact]
    public void ChangeStatus_OnTerminal_ThrowsActivityTerminalException()
    {
        var activity = CreateValid();
        activity.Complete(Now);
        var act = () => activity.ChangeStatus(ActivityStatus.Pending, Now);
        act.Should().Throw<ActivityTerminalException>();
    }

    // ── Idempotência de Complete (DD-004, PBT-02 base) ───────────────────────

    [Fact]
    public void Complete_CalledTwice_DoesNotChangeCompletedAt()
    {
        var activity      = CreateValid();
        var firstComplete = Now;
        activity.Complete(firstComplete);
        var originalCompletedAt = activity.CompletedAt;

        // Segunda chamada: deve ser no-op
        activity.Complete(Now.AddMinutes(5));

        activity.CompletedAt.Should().Be(originalCompletedAt);
    }

    [Fact]
    public void Complete_CalledTwice_DoesNotAccumulateSecondEvent()
    {
        var activity = CreateValid();
        activity.Complete(Now);

        var eventsBeforeSecondCall = activity.DomainEvents.Count;

        activity.Complete(Now.AddMinutes(5)); // no-op

        // Não deve ter acumulado novo ActivityCompleted
        activity.DomainEvents.Count.Should().Be(eventsBeforeSecondCall);
    }

    [Fact]
    public void Complete_FirstCall_AccumulatesActivityCompletedEvent()
    {
        var activity = CreateValid();
        activity.Complete(Now);
        activity.DomainEvents.Should().ContainSingle(e => e is ActivityCompleted);
    }

    // ── Reschedule ───────────────────────────────────────────────────────────

    [Fact]
    public void Reschedule_UpdatesDueAt()
    {
        var activity   = CreateValid();
        var newDueDate = DueDate.Create(Now.AddDays(7));
        activity.Reschedule(newDueDate, Now);
        activity.DueAt.Value.Should().Be(newDueDate.Value);
    }

    [Fact]
    public void Reschedule_DoesNotChangeStatus()
    {
        var activity = CreateValid();
        activity.ChangeStatus(ActivityStatus.InProgress, Now);
        activity.Reschedule(DueDate.Create(Now.AddDays(3)), Now);
        activity.Status.Value.Should().Be("in_progress");
    }

    // ── UpdateDetails ────────────────────────────────────────────────────────

    [Fact]
    public void UpdateDetails_UpdatesTitle()
    {
        var activity = CreateValid();
        activity.UpdateDetails("Novo Título", null, activity.DueAt, null, Now);
        activity.Title.Should().Be("Novo Título");
    }

    [Fact]
    public void UpdateDetails_EmptyTitle_ThrowsTitleRequiredException()
    {
        var activity = CreateValid();
        var act      = () => activity.UpdateDetails("", null, activity.DueAt, null, Now);
        act.Should().Throw<TitleRequiredException>();
    }

    // ── Reconstitute ─────────────────────────────────────────────────────────

    [Fact]
    public void Reconstitute_CreatesActivityWithoutEvents()
    {
        var id = Guid.NewGuid();
        var activity = Activity.Reconstitute(
            id:              id,
            tenantId:        TenantId,
            buId:            BuId,
            ownerId:         OwnerId,
            type:            ActivityType.Create("task"),
            title:           "Tarefa reconstituída",
            dueAt:           DueDate.Create(Now.AddDays(1)),
            status:          ActivityStatus.Pending,
            priority:        Priority.Default,
            completedAt:     null,
            description:     null,
            opportunityLink: null,
            accountLink:     null,
            createdAt:       Now.AddDays(-1),
            updatedAt:       Now);

        activity.Id.Should().Be(id);
        activity.DomainEvents.Should().BeEmpty("reconstituição não gera eventos de domínio");
    }

    // ── IActivityRepository — interface existe no Domain ─────────────────────

    [Fact]
    public void IActivityRepository_ExistsInDomainAssembly()
    {
        var type = typeof(ActivityManagement.Domain.Activities.Repositories.IActivityRepository);
        type.Assembly.GetName().Name.Should().Be("ActivityManagement.Domain");
    }
}

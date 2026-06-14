namespace ActivityManagement.Domain.Tests.Activities.Specifications;

using ActivityManagement.Domain.Activities;
using ActivityManagement.Domain.Activities.Specifications;
using ActivityManagement.Domain.Activities.ValueObjects;

/// <summary>
/// Testes unitários e PBT-04 para <see cref="OverdueSpecification"/>.
/// Propriedade testada: atividade é vencida ⟺ dueAt &lt; referenceInstant E status não terminal.
/// Atividades terminais (completed, cancelled) NUNCA são vencidas.
/// Mapeia: design §4.6, Req 11.1, PBT-04, TASK-05.
/// </summary>
public sealed class OverdueSpecificationTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BuId     = Guid.NewGuid();
    private static readonly Guid OwnerId  = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);

    private static Activity CreateActivity(string status, DateTimeOffset dueAt)
    {
        var activity = Activity.Create(
            tenantId:      TenantId,
            buId:          BuId,
            ownerId:       OwnerId,
            type:          ActivityType.Create("task"),
            title:         "Atividade de teste",
            dueAt:         DueDate.Create(dueAt),
            now:           Now.AddDays(-1),
            correlationId: Guid.NewGuid());

        // Aplica o status desejado via transições
        switch (status)
        {
            case "in_progress":
                activity.ChangeStatus(ActivityStatus.InProgress, Now.AddDays(-1));
                break;
            case "completed":
                activity.Complete(Now.AddDays(-1));
                break;
            case "cancelled":
                activity.Cancel(Now.AddDays(-1));
                break;
            // "pending" é o default — nenhuma transição necessária
        }

        return activity;
    }

    // ── Casos determinísticos ────────────────────────────────────────────────

    [Fact]
    public void Pending_DueAtPast_IsOverdue()
    {
        var spec     = new OverdueSpecification(Now);
        var activity = CreateActivity("pending", Now.AddHours(-1));
        spec.IsSatisfiedBy(activity).Should().BeTrue();
    }

    [Fact]
    public void InProgress_DueAtPast_IsOverdue()
    {
        var spec     = new OverdueSpecification(Now);
        var activity = CreateActivity("in_progress", Now.AddHours(-1));
        spec.IsSatisfiedBy(activity).Should().BeTrue();
    }

    [Fact]
    public void Pending_DueAtFuture_NotOverdue()
    {
        var spec     = new OverdueSpecification(Now);
        var activity = CreateActivity("pending", Now.AddHours(1));
        spec.IsSatisfiedBy(activity).Should().BeFalse();
    }

    [Fact]
    public void Completed_DueAtPast_NeverOverdue()
    {
        var spec     = new OverdueSpecification(Now);
        var activity = CreateActivity("completed", Now.AddDays(-2));
        spec.IsSatisfiedBy(activity).Should().BeFalse("atividade completed nunca é vencida");
    }

    [Fact]
    public void Cancelled_DueAtPast_NeverOverdue()
    {
        var spec     = new OverdueSpecification(Now);
        var activity = CreateActivity("cancelled", Now.AddDays(-2));
        spec.IsSatisfiedBy(activity).Should().BeFalse("atividade cancelled nunca é vencida");
    }

    [Fact]
    public void Pending_DueAtExactlyNow_NotOverdue()
    {
        // Limite: dueAt == referenceInstant → não é vencida (somente < é vencida)
        var spec     = new OverdueSpecification(Now);
        var activity = CreateActivity("pending", Now);
        spec.IsSatisfiedBy(activity).Should().BeFalse("dueAt == now não é vencida");
    }
}

/// <summary>
/// PBT-04 — Invariante de atividade vencida.
/// Gera pares (dueAt, status) arbitrários e verifica:
///   - terminais NUNCA são vencidas;
///   - não-terminais com dueAt &lt; referência são SEMPRE vencidas.
/// Mapeia: PBT-04, design §13, TASK-05 ST-01.
/// </summary>
public sealed class OverdueSpecificationPbt04Tests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BuId     = Guid.NewGuid();
    private static readonly Guid OwnerId  = Guid.NewGuid();
    private static readonly DateTimeOffset Reference = new(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] AllStatuses = ["pending", "in_progress", "completed", "cancelled"];
    private static readonly string[] TerminalStatuses = ["completed", "cancelled"];
    private static readonly string[] NonTerminalStatuses = ["pending", "in_progress"];

    private static Activity BuildWithStatus(string status, DateTimeOffset dueAt)
    {
        var activity = Activity.Create(
            tenantId:      TenantId,
            buId:          BuId,
            ownerId:       OwnerId,
            type:          ActivityType.Create("task"),
            title:         "PBT",
            dueAt:         DueDate.Create(dueAt),
            now:           Reference.AddDays(-2),
            correlationId: Guid.NewGuid());

        switch (status)
        {
            case "in_progress": activity.ChangeStatus(ActivityStatus.InProgress, Reference.AddDays(-2)); break;
            case "completed":   activity.Complete(Reference.AddDays(-2)); break;
            case "cancelled":   activity.Cancel(Reference.AddDays(-2)); break;
        }

        return activity;
    }

    /// <summary>
    /// PBT-04a: atividade terminal com qualquer dueAt nunca é vencida.
    /// </summary>
    [Property(MaxTest = 500)]
    public Property Terminal_NeverOverdue(PositiveInt daysOffset, bool isPast)
    {
        var statusIdx  = daysOffset.Get % TerminalStatuses.Length;
        var status     = TerminalStatuses[statusIdx];
        var dueAt      = isPast
            ? Reference.AddDays(-(daysOffset.Get % 365 + 1))
            : Reference.AddDays(daysOffset.Get % 365 + 1);

        var spec     = new OverdueSpecification(Reference);
        var activity = BuildWithStatus(status, dueAt);
        return (!spec.IsSatisfiedBy(activity)).ToProperty();
    }

    /// <summary>
    /// PBT-04b: atividade não terminal com dueAt estritamente anterior à referência é sempre vencida.
    /// </summary>
    [Property(MaxTest = 500)]
    public Property NonTerminal_DueAtPast_IsOverdue(PositiveInt daysOffset)
    {
        var statusIdx  = daysOffset.Get % NonTerminalStatuses.Length;
        var status     = NonTerminalStatuses[statusIdx];
        var dueAt      = Reference.AddDays(-(daysOffset.Get % 365 + 1)); // sempre no passado

        var spec     = new OverdueSpecification(Reference);
        var activity = BuildWithStatus(status, dueAt);
        return spec.IsSatisfiedBy(activity).ToProperty();
    }

    /// <summary>
    /// PBT-04c: atividade não terminal com dueAt futuro nunca é vencida.
    /// </summary>
    [Property(MaxTest = 500)]
    public Property NonTerminal_DueAtFuture_NotOverdue(PositiveInt daysOffset)
    {
        var statusIdx  = daysOffset.Get % NonTerminalStatuses.Length;
        var status     = NonTerminalStatuses[statusIdx];
        var dueAt      = Reference.AddDays(daysOffset.Get % 365 + 1); // sempre no futuro

        var spec     = new OverdueSpecification(Reference);
        var activity = BuildWithStatus(status, dueAt);
        return (!spec.IsSatisfiedBy(activity)).ToProperty();
    }
}

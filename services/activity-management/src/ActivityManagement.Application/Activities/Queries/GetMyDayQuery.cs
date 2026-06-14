namespace ActivityManagement.Application.Activities.Queries;

using ActivityManagement.Application.Common;
using ActivityManagement.Application.Ports;
using ActivityManagement.Domain.Activities;
using ActivityManagement.Domain.Activities.Repositories;
using ActivityManagement.Domain.Activities.ValueObjects;
using MediatR;

/// <summary>
/// Resultado agrupado da visão "Meu dia" do vendedor.
/// </summary>
/// <param name="Overdue">Atividades vencidas (dueAt &lt; início do dia local do tenant).</param>
/// <param name="Today">Atividades com dueAt no dia corrente do tenant.</param>
/// <param name="Upcoming">Atividades com dueAt após o dia corrente do tenant.</param>
public sealed record MyDayResult(
    IReadOnlyList<Activity> Overdue,
    IReadOnlyList<Activity> Today,
    IReadOnlyList<Activity> Upcoming);

/// <summary>
/// Query para obter a visão "Meu dia" do vendedor autenticado: atividades agrupadas
/// em três faixas (vencidas, hoje, próximas) calculadas no fuso IANA do tenant (DD-008, Req 5).
/// Atividades terminais nunca aparecem em nenhuma faixa.
/// Mapeia: design §5.2, Req 5, DD-008, TASK-11.
/// </summary>
public sealed record GetMyDayQuery()
    : IRequest<MyDayResult>, ITenantRequest
{
    /// <inheritdoc />
    public TenantContext? TenantContext { get; set; }
}

/// <summary>
/// Handler do <see cref="GetMyDayQuery"/>.
/// </summary>
internal sealed class GetMyDayQueryHandler : IRequestHandler<GetMyDayQuery, MyDayResult>
{
    private readonly IActivityRepository _repository;
    private readonly ITenantClock        _tenantClock;

    public GetMyDayQueryHandler(IActivityRepository repository, ITenantClock tenantClock)
    {
        _repository  = repository;
        _tenantClock = tenantClock;
    }

    public async Task<MyDayResult> Handle(GetMyDayQuery query, CancellationToken cancellationToken)
    {
        var ctx           = query.TenantContext!;
        var startOfDay    = _tenantClock.GetStartOfDayForTenant(ctx.TenantId);
        var endOfDay      = startOfDay.AddDays(1);
        var endOfWeek     = _tenantClock.GetEndOfWeekForTenant(ctx.TenantId);

        // Vencidas: dueAt < startOfDay (OverdueSpecification)
        var overdueTask = _repository.GetOverdueByOwnerAsync(ctx.UserId, startOfDay, cancellationToken);

        // Hoje + próximas: dueAt ∈ [startOfDay, endOfWeek)
        var rangeTask = _repository.GetByOwnerInRangeAsync(ctx.UserId, startOfDay, endOfWeek, cancellationToken);

        await Task.WhenAll(overdueTask, rangeTask);

        var overdue  = overdueTask.Result.Where(a => !a.Status.IsTerminal).ToList();
        var inRange  = rangeTask.Result.Where(a => !a.Status.IsTerminal).ToList();
        var today    = inRange.Where(a => a.DueAt.Value < endOfDay).ToList();
        var upcoming = inRange.Where(a => a.DueAt.Value >= endOfDay).ToList();

        return new MyDayResult(
            Overdue:  overdue,
            Today:    today,
            Upcoming: upcoming);
    }
}

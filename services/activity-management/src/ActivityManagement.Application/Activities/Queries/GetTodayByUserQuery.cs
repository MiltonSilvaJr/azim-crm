namespace ActivityManagement.Application.Activities.Queries;

using ActivityManagement.Application.Common;
using ActivityManagement.Application.Ports;
using ActivityManagement.Domain.Activities;
using ActivityManagement.Domain.Activities.Repositories;
using MediatR;

/// <summary>
/// Query para obter atividades do dia do usuário (para digest e visão do vendedor).
/// A faixa "hoje" é calculada no fuso IANA do tenant (DD-008, Req 11.4).
/// Atividades terminais são excluídas.
/// Mapeia: design §5.2, Req 11.4, Req 5.6, DD-008, TASK-12.
/// </summary>
/// <param name="UserId">Identificador do usuário cujas atividades do dia serão retornadas.</param>
public sealed record GetTodayByUserQuery(Guid UserId)
    : IRequest<IReadOnlyList<Activity>>, ITenantRequest
{
    /// <inheritdoc />
    public TenantContext? TenantContext { get; set; }
}

/// <summary>
/// Handler do <see cref="GetTodayByUserQuery"/>.
/// </summary>
internal sealed class GetTodayByUserQueryHandler
    : IRequestHandler<GetTodayByUserQuery, IReadOnlyList<Activity>>
{
    private readonly IActivityRepository _repository;
    private readonly ITenantClock        _tenantClock;

    public GetTodayByUserQueryHandler(IActivityRepository repository, ITenantClock tenantClock)
    {
        _repository  = repository;
        _tenantClock = tenantClock;
    }

    public async Task<IReadOnlyList<Activity>> Handle(
        GetTodayByUserQuery query,
        CancellationToken   cancellationToken)
    {
        var ctx        = query.TenantContext!;
        var startOfDay = _tenantClock.GetStartOfDayForTenant(ctx.TenantId);
        var endOfDay   = startOfDay.AddDays(1);

        var activities = await _repository.GetByOwnerInRangeAsync(
            query.UserId, startOfDay, endOfDay, cancellationToken);

        return activities.Where(a => !a.Status.IsTerminal).ToList();
    }
}

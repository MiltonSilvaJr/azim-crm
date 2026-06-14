namespace ActivityManagement.Application.Activities.Queries;

using ActivityManagement.Application.Common;
using ActivityManagement.Application.Ports;
using ActivityManagement.Domain.Activities;
using ActivityManagement.Domain.Activities.Repositories;
using MediatR;

/// <summary>
/// Query para obter a visão "Minha semana" do vendedor autenticado.
/// Retorna atividades não terminais com <c>dueAt</c> na semana corrente do tenant (Req 5, DD-008).
/// Mapeia: design §5.2, Req 5, DD-008, TASK-11.
/// </summary>
public sealed record GetMyWeekQuery()
    : IRequest<IReadOnlyList<Activity>>, ITenantRequest
{
    /// <inheritdoc />
    public TenantContext? TenantContext { get; set; }
}

/// <summary>
/// Handler do <see cref="GetMyWeekQuery"/>.
/// </summary>
internal sealed class GetMyWeekQueryHandler : IRequestHandler<GetMyWeekQuery, IReadOnlyList<Activity>>
{
    private readonly IActivityRepository _repository;
    private readonly ITenantClock        _tenantClock;

    public GetMyWeekQueryHandler(IActivityRepository repository, ITenantClock tenantClock)
    {
        _repository  = repository;
        _tenantClock = tenantClock;
    }

    public async Task<IReadOnlyList<Activity>> Handle(
        GetMyWeekQuery    query,
        CancellationToken cancellationToken)
    {
        var ctx      = query.TenantContext!;
        var weekStart = _tenantClock.GetStartOfWeekForTenant(ctx.TenantId);
        var weekEnd   = _tenantClock.GetEndOfWeekForTenant(ctx.TenantId);

        var activities = await _repository.GetByOwnerInRangeAsync(
            ctx.UserId, weekStart, weekEnd, cancellationToken);

        // Filtra terminais (defesa adicional — o repositório já deve aplicar, mas garantimos)
        return activities.Where(a => !a.Status.IsTerminal).ToList();
    }
}

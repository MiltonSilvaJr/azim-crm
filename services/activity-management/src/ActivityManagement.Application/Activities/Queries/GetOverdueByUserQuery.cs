namespace ActivityManagement.Application.Activities.Queries;

using ActivityManagement.Application.Common;
using ActivityManagement.Domain.Activities;
using ActivityManagement.Domain.Activities.Repositories;
using MediatR;

/// <summary>
/// Query para obter atividades vencidas do usuário (para digest e visão do vendedor).
/// Atividades terminais são excluídas (Req 11.1).
/// Mapeia: design §5.2, Req 11.3, Req 5.6, TASK-12.
/// </summary>
/// <param name="UserId">Identificador do usuário cujas atividades vencidas serão retornadas.</param>
public sealed record GetOverdueByUserQuery(Guid UserId)
    : IRequest<IReadOnlyList<Activity>>, ITenantRequest
{
    /// <inheritdoc />
    public TenantContext? TenantContext { get; set; }
}

/// <summary>
/// Handler do <see cref="GetOverdueByUserQuery"/>.
/// </summary>
internal sealed class GetOverdueByUserQueryHandler
    : IRequestHandler<GetOverdueByUserQuery, IReadOnlyList<Activity>>
{
    private readonly IActivityRepository _repository;

    public GetOverdueByUserQueryHandler(IActivityRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<Activity>> Handle(
        GetOverdueByUserQuery query,
        CancellationToken     cancellationToken)
    {
        var now        = DateTimeOffset.UtcNow;
        var activities = await _repository.GetOverdueByOwnerAsync(query.UserId, now, cancellationToken);

        // Filtra terminais (defesa adicional — Req 11.1)
        return activities.Where(a => !a.Status.IsTerminal).ToList();
    }
}

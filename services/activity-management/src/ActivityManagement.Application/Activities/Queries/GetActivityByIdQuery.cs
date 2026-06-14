namespace ActivityManagement.Application.Activities.Queries;

using ActivityManagement.Application.Activities.Commands;
using ActivityManagement.Application.Common;
using ActivityManagement.Contracts.Activities;
using ActivityManagement.Domain.Activities.Repositories;
using MediatR;

/// <summary>
/// Query para obter o detalhe de uma atividade por ID (design §8, Req 2).
/// Retorna <see cref="ActivityResponse"/> ou lança <see cref="ActivityNotFoundException"/>
/// quando a atividade não existe ou está fora do tenant/escopo (ACT-ERR-003, anti-enumeração).
/// Mapeia: design §5.2, design §8, ACT-ERR-003, TASK-18.
/// </summary>
/// <param name="ActivityId">Identificador da atividade.</param>
public sealed record GetActivityByIdQuery(Guid ActivityId)
    : IRequest<ActivityResponse>, ITenantRequest
{
    /// <inheritdoc />
    public TenantContext? TenantContext { get; set; }
}

/// <summary>
/// Handler da <see cref="GetActivityByIdQuery"/>.
/// </summary>
internal sealed class GetActivityByIdQueryHandler
    : IRequestHandler<GetActivityByIdQuery, ActivityResponse>
{
    private readonly IActivityRepository _repository;

    public GetActivityByIdQueryHandler(IActivityRepository repository)
    {
        _repository = repository;
    }

    public async Task<ActivityResponse> Handle(
        GetActivityByIdQuery query,
        CancellationToken    cancellationToken)
    {
        var activity = await _repository.FindByIdAsync(query.ActivityId, cancellationToken)
            ?? throw new ActivityNotFoundException(query.ActivityId);

        return ActivityMapper.ToResponse(activity);
    }
}

namespace ActivityManagement.Application.Activities.Queries;

using ActivityManagement.Application.Common;
using ActivityManagement.Domain.Activities;
using ActivityManagement.Domain.Activities.Repositories;
using MediatR;

/// <summary>
/// Query para obter a última atividade concluída por oportunidade em lote.
/// Suportado pelo índice <c>(tenant_id, opportunity_id, completed_at)</c> — sem table scan (RNF 4).
/// Mapeia: design §5.2, Req 12, RNF 4.3, TASK-12.
/// </summary>
/// <param name="OpportunityIds">Lista de IDs de oportunidades a consultar.</param>
public sealed record GetLastCompletedActivityQuery(IReadOnlyList<Guid> OpportunityIds)
    : IRequest<IReadOnlyDictionary<Guid, Activity>>, ITenantRequest
{
    /// <inheritdoc />
    public TenantContext? TenantContext { get; set; }
}

/// <summary>
/// Handler do <see cref="GetLastCompletedActivityQuery"/>.
/// </summary>
internal sealed class GetLastCompletedActivityQueryHandler
    : IRequestHandler<GetLastCompletedActivityQuery, IReadOnlyDictionary<Guid, Activity>>
{
    private readonly IActivityRepository _repository;

    public GetLastCompletedActivityQueryHandler(IActivityRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyDictionary<Guid, Activity>> Handle(
        GetLastCompletedActivityQuery query,
        CancellationToken             cancellationToken)
    {
        if (query.OpportunityIds.Count == 0)
            return new Dictionary<Guid, Activity>();

        // Consulta única ao repositório — sem N+1 (RNF 4.3)
        return await _repository.GetLastCompletedByOpportunitiesAsync(
            query.OpportunityIds, cancellationToken);
    }
}

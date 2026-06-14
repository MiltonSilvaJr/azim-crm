namespace ActivityManagement.Application.Activities.Queries;

using ActivityManagement.Application.Common;
using ActivityManagement.Application.Ports;
using ActivityManagement.Domain.Activities.Repositories;
using MediatR;

/// <summary>
/// Query para obter oportunidades abertas sem atividade não terminal futura vinculada.
/// Sinalização de saúde do funil (Req 10, DD-006 — não bloqueia operações).
/// Mapeia: design §5.2, Req 10, PBT-05, TASK-11.
/// </summary>
public sealed record GetOpportunitiesWithoutFollowupQuery()
    : IRequest<IReadOnlyList<Guid>>, ITenantRequest
{
    /// <inheritdoc />
    public TenantContext? TenantContext { get; set; }
}

/// <summary>
/// Handler do <see cref="GetOpportunitiesWithoutFollowupQuery"/>.
/// </summary>
internal sealed class GetOpportunitiesWithoutFollowupQueryHandler
    : IRequestHandler<GetOpportunitiesWithoutFollowupQuery, IReadOnlyList<Guid>>
{
    private readonly IActivityRepository  _repository;
    private readonly IOpportunityReadPort _opportunityPort;

    public GetOpportunitiesWithoutFollowupQueryHandler(
        IActivityRepository  repository,
        IOpportunityReadPort opportunityPort)
    {
        _repository      = repository;
        _opportunityPort = opportunityPort;
    }

    public async Task<IReadOnlyList<Guid>> Handle(
        GetOpportunitiesWithoutFollowupQuery query,
        CancellationToken                    cancellationToken)
    {
        var ctx  = query.TenantContext!;
        var now  = DateTimeOffset.UtcNow;

        // Obtém oportunidades abertas da BU
        var openIds = await _opportunityPort.GetOpenOpportunityIdsForBuAsync(
            ctx.BuId, ctx.TenantId, cancellationToken);

        if (openIds.Count == 0)
            return [];

        // Delega ao repositório a verificação de follow-up futuro (suportado por índice)
        return await _repository.GetOpportunityIdsWithoutFollowupAsync(
            ctx.BuId, openIds, now, cancellationToken);
    }
}

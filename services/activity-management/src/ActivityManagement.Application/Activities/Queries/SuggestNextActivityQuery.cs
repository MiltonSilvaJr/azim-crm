namespace ActivityManagement.Application.Activities.Queries;

using ActivityManagement.Application.Common;
using ActivityManagement.Application.Ports;
using ActivityManagement.Domain.Activities.Repositories;
using MediatR;

/// <summary>
/// Sugestão de vínculo retornada por <see cref="SuggestNextActivityQuery"/>.
/// Nunca cria atividade — apenas retorna pré-preenchimento de vínculo (Req 9.4, DD-006).
/// </summary>
/// <param name="OpportunityId">ID da oportunidade sugerida para a próxima atividade.</param>
/// <param name="AccountId">ID da conta vinculada à oportunidade (quando disponível).</param>
public sealed record NextActivitySuggestion(
    Guid  OpportunityId,
    Guid? AccountId);

/// <summary>
/// Query para sugestão de próxima atividade ao concluir uma atividade vinculada a oportunidade aberta.
/// Não cria atividade (Req 9.4, DD-006). Retorna <c>null</c> quando:
///   - atividade não está vinculada a oportunidade;
///   - oportunidade está fechada (Req 9.5).
/// Mapeia: design §5.2, Req 9, PBT-05, TASK-08.
/// </summary>
/// <param name="ActivityId">Identificador da atividade recém-concluída.</param>
public sealed record SuggestNextActivityQuery(Guid ActivityId)
    : IRequest<NextActivitySuggestion?>, ITenantRequest
{
    /// <inheritdoc />
    public TenantContext? TenantContext { get; set; }
}

/// <summary>
/// Handler da <see cref="SuggestNextActivityQuery"/>.
/// </summary>
internal sealed class SuggestNextActivityQueryHandler
    : IRequestHandler<SuggestNextActivityQuery, NextActivitySuggestion?>
{
    private readonly IActivityRepository  _repository;
    private readonly IOpportunityReadPort _opportunityPort;

    public SuggestNextActivityQueryHandler(
        IActivityRepository  repository,
        IOpportunityReadPort opportunityPort)
    {
        _repository      = repository;
        _opportunityPort = opportunityPort;
    }

    public async Task<NextActivitySuggestion?> Handle(
        SuggestNextActivityQuery query,
        CancellationToken        cancellationToken)
    {
        var ctx = query.TenantContext!;

        var activity = await _repository.FindByIdAsync(query.ActivityId, cancellationToken);
        if (activity is null)
            return null;

        // Sem vínculo de oportunidade → sem sugestão (Req 9.5)
        if (activity.OpportunityLink is null)
            return null;

        var opportunityId = activity.OpportunityLink.OpportunityId;

        // Oportunidade fechada → sem sugestão (Req 9.5)
        var isOpen = await _opportunityPort.IsOpenAsync(
            opportunityId, ctx.TenantId, cancellationToken);

        if (!isOpen)
            return null;

        return new NextActivitySuggestion(
            OpportunityId: opportunityId,
            AccountId:     activity.AccountLink?.AccountId);
    }
}

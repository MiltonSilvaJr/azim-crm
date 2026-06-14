using GoalForecast.Application.Ports;
using GoalForecast.Domain.Services;
using GoalForecast.Domain.ValueObjects;
using MediatR;
using AppException = GoalForecast.Application.Common.ApplicationException;

namespace GoalForecast.Application.Queries;

/// <summary>
/// Handler que delega a soma de metas ao serviço de domínio <see cref="GoalAggregation"/>.
/// O handler é orquestrador puro: carrega metas do repositório e delega a lógica de soma
/// ao domínio. Nunca persiste metas trimestrais/anuais (DD-003, RN-027).
///
/// Mapeia: Req 7, PBT-02, DD-003, RN-027, design §5.2, TASK-13.
/// </summary>
public sealed class GetGoalAggregateQueryHandler
    : IRequestHandler<GetGoalAggregateQuery, GoalAggregateResult>
{
    private readonly IGoalRepository _repository;

    /// <summary>Inicializa com o repositório de metas.</summary>
    public GetGoalAggregateQueryHandler(IGoalRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc/>
    public async Task<GoalAggregateResult> Handle(
        GetGoalAggregateQuery query,
        CancellationToken cancellationToken)
    {
        var tenantId = query.Principal.TenantId;

        // Carrega as metas do ano para o scope (≤ 12 itens, DD-003).
        var goals = await _repository.ListByYear(
            tenantId, query.BuId, query.OwnerId, query.Year, cancellationToken);

        Money total;
        int? quarter = null;

        switch (query.Granularity)
        {
            case AggregateGranularity.Quarter:
                if (!query.Quarter.HasValue || query.Quarter.Value < 1 || query.Quarter.Value > 4)
                    throw new AppException("GF-ERR-002",
                        "Mês ou ano fora da faixa: Quarter deve estar entre 1 e 4.", 400);

                quarter = query.Quarter.Value;
                // Usa o primeiro mês do trimestre como referência de período.
                var firstMonth = (quarter.Value - 1) * 3 + 1;
                var period = new GoalPeriod(query.Year, firstMonth);
                total = GoalAggregation.SumByQuarter(goals, period);
                break;

            case AggregateGranularity.Year:
                total = GoalAggregation.SumByYear(goals, query.Year);
                break;

            default:
                throw new AppException("GF-ERR-002",
                    $"Granularidade inválida: '{query.Granularity}'.", 400);
        }

        return new GoalAggregateResult(
            Granularity: query.Granularity.ToString().ToLowerInvariant(),
            Year: query.Year,
            Quarter: quarter,
            ValorMetaAgregado: total.Cents);
    }
}

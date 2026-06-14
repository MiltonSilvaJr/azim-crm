using MediatR;
using Reporting.Application.Ports;
using Reporting.Contracts.Responses;

namespace Reporting.Application.Queries.Forecast;

/// <summary>
/// Handler do relatório de forecast por BU/mês (Req 6).
///
/// Responsabilidades:
/// <list type="number">
///   <item><description>Chama <see cref="IReportingReadRepository.GetForecastAsync"/> com escopo e período.</description></item>
///   <item><description>Retorna <see cref="ForecastReportResponse"/> com as linhas do repositório.</description></item>
///   <item><description>Não recalcula valores — reflete o repositório (P2).</description></item>
///   <item><description>Preserva <c>GoalCents = null</c> quando meta ausente — nunca substitui por zero (degradação graciosa, Req 6.3).</description></item>
/// </list>
///
/// Mapeia: TASK-07, design §5.3, Req 6, Req 6.3, DD-007, P8.
/// </summary>
public sealed class GetForecastReportQueryHandler : IRequestHandler<GetForecastReportQuery, ForecastReportResponse>
{
    private readonly IReportingReadRepository _repository;

    /// <summary>Inicializa o handler com o repositório de leitura.</summary>
    public GetForecastReportQueryHandler(IReportingReadRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <inheritdoc/>
    public async Task<ForecastReportResponse> Handle(
        GetForecastReportQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rows = await _repository.GetForecastAsync(
            request.Scope,
            request.Period,
            request.BuIds,
            cancellationToken);

        // GoalCents é null quando meta ausente — a view usa LEFT JOIN (DD-005, Req 6.3).
        // O handler apenas repassa o valor; nunca substitui null por zero.
        return new ForecastReportResponse(rows);
    }
}

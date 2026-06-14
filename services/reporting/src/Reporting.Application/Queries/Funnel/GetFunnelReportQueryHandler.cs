using MediatR;
using Reporting.Application.Ports;
using Reporting.Contracts.Responses;

namespace Reporting.Application.Queries.Funnel;

/// <summary>
/// Handler do relatório de funil por estágio (Req 1).
///
/// Responsabilidades:
/// <list type="number">
///   <item><description>Chama <see cref="IReportingReadRepository.GetFunnelAsync"/> com o escopo e período recebidos.</description></item>
///   <item><description>Mapeia as linhas brutas para <see cref="FunnelReportResponse"/> preservando centavos inteiros (DD-007).</description></item>
/// </list>
///
/// Não recalcula <c>forecast_ponderado</c> nem <c>valor_total</c> — reflete o valor do repositório (P2).
/// O escopo RBAC já foi resolvido e validado pelos behaviors (design §5.3, §5.4).
///
/// Mapeia: TASK-06, design §5.3, Req 1, DD-007.
/// </summary>
public sealed class GetFunnelReportQueryHandler : IRequestHandler<GetFunnelReportQuery, FunnelReportResponse>
{
    private readonly IReportingReadRepository _repository;

    /// <summary>Inicializa o handler com o repositório de leitura.</summary>
    public GetFunnelReportQueryHandler(IReportingReadRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <inheritdoc/>
    public async Task<FunnelReportResponse> Handle(GetFunnelReportQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rows = await _repository.GetFunnelAsync(
            request.Scope,
            request.Period,
            request.BuIds,
            cancellationToken);

        return new FunnelReportResponse(rows);
    }
}

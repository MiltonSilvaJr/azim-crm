using MediatR;
using Reporting.Application.Policies;
using Reporting.Application.Ports;
using Reporting.Contracts.ReadModels;
using Reporting.Contracts.Responses;
using Reporting.Domain.Enums;

namespace Reporting.Application.Queries.Ranking;

/// <summary>
/// Handler do relatório de ranking por responsável (Req 2).
///
/// Responsabilidades:
/// <list type="number">
///   <item><description>Chama <see cref="IReportingReadRepository.GetRankingAsync"/> com escopo e período.</description></item>
///   <item><description>Aplica <see cref="PiiMinimizationPolicy"/> para omitir <c>DisplayName</c> quando não permitido (DD-008, RNF 4).</description></item>
///   <item><description>Filtra para que Vendedor receba apenas a própria linha (Req 2.4).</description></item>
///   <item><description>Ordena por <c>WonValueCents</c> decrescente (Req 2.2).</description></item>
///   <item><description>Nunca registra <c>DisplayName</c> em logs.</description></item>
/// </list>
///
/// Mapeia: TASK-08, design §5.3, Req 2, Req 2.2, Req 2.4, DD-007, DD-008, RNF 4.
/// </summary>
public sealed class GetRankingReportQueryHandler : IRequestHandler<GetRankingReportQuery, RankingReportResponse>
{
    private readonly IReportingReadRepository _repository;
    private readonly PiiMinimizationPolicy    _piiPolicy;

    /// <summary>Inicializa o handler com o repositório e a política de PII.</summary>
    public GetRankingReportQueryHandler(
        IReportingReadRepository repository,
        PiiMinimizationPolicy piiPolicy)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(piiPolicy);
        _repository = repository;
        _piiPolicy  = piiPolicy;
    }

    /// <inheritdoc/>
    public async Task<RankingReportResponse> Handle(
        GetRankingReportQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rawRows = await _repository.GetRankingAsync(
            request.Scope,
            request.Period,
            request.BuIds,
            cancellationToken);

        // Vendedor: filtra para retornar apenas a própria linha (Req 2.4)
        var filteredRows = request.Scope.Role == ReportingRole.Vendedor
            ? rawRows.Where(r => r.OwnerId == request.Scope.OwnerRestrictedTo).ToList()
            : rawRows.ToList();

        // Ordenação por WonValueCents desc (Req 2.2)
        var ordered = filteredRows
            .OrderByDescending(r => r.WonValueCents)
            .Select(r => ApplyPiiPolicy(r, request.Scope))
            .ToList();

        return new RankingReportResponse(ordered);
    }

    private RankingRow ApplyPiiPolicy(RankingRow row, Domain.ValueObjects.ReportScope scope) =>
        row with { DisplayName = _piiPolicy.ApplyTo(row.DisplayName, scope) };
}

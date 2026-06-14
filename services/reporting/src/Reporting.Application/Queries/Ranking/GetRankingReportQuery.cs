using MediatR;
using Reporting.Contracts.Responses;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Queries.Ranking;

/// <summary>
/// Query para o relatório de ranking por responsável (Req 2).
///
/// Vendedor recebe apenas a própria linha (Req 2.4).
/// <c>display_name</c> incluído somente no escopo RBAC correto, via <c>PiiMinimizationPolicy</c> (DD-008, RNF 4).
///
/// Mapeia: TASK-08, design §5.2, Req 2, DD-008.
/// </summary>
public sealed record GetRankingReportQuery(
    Period Period,
    IEnumerable<Guid>? BuIds,
    ReportScope Scope) : IRequest<RankingReportResponse>;

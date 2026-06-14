using MediatR;
using Reporting.Application.Behaviors;
using Reporting.Contracts.Responses;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Queries.Ranking;

/// <summary>
/// Query para o relatório de ranking por responsável (Req 2).
///
/// Vendedor recebe apenas a própria linha (Req 2.4).
/// <c>display_name</c> incluído somente no escopo RBAC correto, via <c>PiiMinimizationPolicy</c> (DD-008, RNF 4).
///
/// Implementa <see cref="IScopedQuery"/> para validação de escopo no <c>AuthorizationBehavior</c>
/// sem chamada dupla ao <c>IScopeResolver</c> (dívida Onda 5).
///
/// Mapeia: TASK-08, Onda 6 (refactor), design §5.2, Req 2, DD-008, ADR-0001.
/// </summary>
public sealed record GetRankingReportQuery(
    Period Period,
    IEnumerable<Guid>? BuIds,
    ReportScope Scope) : IRequest<RankingReportResponse>, IScopedQuery;

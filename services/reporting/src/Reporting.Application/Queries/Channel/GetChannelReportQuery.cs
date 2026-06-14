using MediatR;
using Reporting.Application.Behaviors;
using Reporting.Contracts.Responses;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Queries.Channel;

/// <summary>
/// Query para o relatório de oportunidades por canal (Req 3).
///
/// Percentuais em basis points inteiros (base 10.000 = 100%) — sem float (DD-010, PBT-04).
///
/// Implementa <see cref="IScopedQuery"/> para validação de escopo no <c>AuthorizationBehavior</c>
/// sem chamada dupla ao <c>IScopeResolver</c> (dívida Onda 5).
///
/// Mapeia: TASK-09, Onda 6 (refactor), design §5.2, Req 3, DD-010, ADR-0001.
/// </summary>
public sealed record GetChannelReportQuery(
    Period Period,
    IEnumerable<Guid>? BuIds,
    ReportScope Scope) : IRequest<ChannelReportResponse>, IScopedQuery;

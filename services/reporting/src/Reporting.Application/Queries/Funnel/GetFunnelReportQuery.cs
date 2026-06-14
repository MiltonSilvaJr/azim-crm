using MediatR;
using Reporting.Application.Behaviors;
using Reporting.Contracts.Responses;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Queries.Funnel;

/// <summary>
/// Query para o relatório de funil por estágio (Req 1).
///
/// O <see cref="Scope"/> é resolvido no servidor pelo <c>ReportDispatcher</c> via <c>IScopeResolver</c>
/// antes de chegar ao pipeline — nunca vem do cliente (Req 7, DD-006).
///
/// Implementa <see cref="IScopedQuery"/> para que o <c>AuthorizationBehavior</c> valide o escopo
/// sem invocar novamente o <c>IScopeResolver</c> (elimina chamada dupla — dívida Onda 5).
///
/// Mapeia: TASK-06, Onda 6 (refactor), design §5.2, Req 1, ADR-0001.
/// </summary>
public sealed record GetFunnelReportQuery(
    Period Period,
    IEnumerable<Guid>? BuIds,
    ReportScope Scope) : IRequest<FunnelReportResponse>, IScopedQuery;

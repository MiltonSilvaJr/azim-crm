using MediatR;
using Reporting.Contracts.Responses;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Queries.Funnel;

/// <summary>
/// Query para o relatório de funil por estágio (Req 1).
///
/// O <see cref="Scope"/> é resolvido no servidor pelo <c>AuthorizationBehavior</c>
/// antes de chegar ao handler — nunca vem do cliente (Req 7, DD-006).
///
/// Mapeia: TASK-06, design §5.2, Req 1.
/// </summary>
public sealed record GetFunnelReportQuery(
    Period Period,
    IEnumerable<Guid>? BuIds,
    ReportScope Scope) : IRequest<FunnelReportResponse>;

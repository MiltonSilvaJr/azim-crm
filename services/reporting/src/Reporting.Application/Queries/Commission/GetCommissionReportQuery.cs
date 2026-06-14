using MediatR;
using Reporting.Contracts.Responses;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Queries.Commission;

/// <summary>
/// Query para o relatório de comissões por parceiro (Req 4).
///
/// <c>consolidatedCents</c> derivado exclusivamente de <c>isSnapshot = true</c> (RN-007, Req 4.2).
/// <c>projectedCents</c> derivado de <c>isSnapshot = false</c> com <c>stageCategory = open</c> (Req 4.3).
///
/// Mapeia: TASK-10, design §5.2, Req 4, PBT-01, PBT-02, RN-007.
/// </summary>
public sealed record GetCommissionReportQuery(
    Period Period,
    IEnumerable<Guid>? BuIds,
    ReportScope Scope) : IRequest<CommissionReportResponse>;

using MediatR;
using Reporting.Application.Behaviors;
using Reporting.Contracts.Responses;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Queries.Commission;

/// <summary>
/// Query para o relatório de comissões por parceiro (Req 4).
///
/// <c>consolidatedCents</c> derivado exclusivamente de <c>isSnapshot = true</c> (RN-007, Req 4.2).
/// <c>projectedCents</c> derivado de <c>isSnapshot = false</c> com <c>stageCategory = open</c> (Req 4.3).
///
/// Implementa <see cref="IScopedQuery"/> para validação de escopo no <c>AuthorizationBehavior</c>
/// sem chamada dupla ao <c>IScopeResolver</c> (dívida Onda 5).
///
/// Mapeia: TASK-10, Onda 6 (refactor), design §5.2, Req 4, PBT-01, PBT-02, RN-007, ADR-0001.
/// </summary>
public sealed record GetCommissionReportQuery(
    Period Period,
    IEnumerable<Guid>? BuIds,
    ReportScope Scope) : IRequest<CommissionReportResponse>, IScopedQuery;

using MediatR;
using Reporting.Application.Behaviors;
using Reporting.Contracts.Responses;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Queries.Forecast;

/// <summary>
/// Query para o relatório de forecast por BU/mês (Req 6).
///
/// Suporta comparativo com meta opcional: quando a meta está ausente, retorna <c>goalCents = null</c>
/// sem lançar erro (degradação graciosa — Req 6.3, P8).
///
/// O <see cref="Scope"/> é resolvido no servidor pelo <c>ReportDispatcher</c> via <c>IScopeResolver</c>
/// — nunca vem do cliente (DD-006). Implementa <see cref="IScopedQuery"/> para validação de escopo
/// no <c>AuthorizationBehavior</c> sem chamada dupla ao resolver.
///
/// Mapeia: TASK-07, Onda 6 (refactor), design §5.2, Req 6, Req 6.3, ADR-0001.
/// </summary>
public sealed record GetForecastReportQuery(
    Period Period,
    IEnumerable<Guid>? BuIds,
    ReportScope Scope) : IRequest<ForecastReportResponse>, IScopedQuery;

using MediatR;
using Reporting.Contracts.Responses;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Queries.Forecast;

/// <summary>
/// Query para o relatório de forecast por BU/mês (Req 6).
///
/// Suporta comparativo com meta opcional: quando a meta está ausente, retorna <c>goalCents = null</c>
/// sem lançar erro (degradação graciosa — Req 6.3, P8).
///
/// O <see cref="Scope"/> é resolvido no servidor pelos behaviors — nunca vem do cliente (DD-006).
///
/// Mapeia: TASK-07, design §5.2, Req 6, Req 6.3.
/// </summary>
public sealed record GetForecastReportQuery(
    Period Period,
    IEnumerable<Guid>? BuIds,
    ReportScope Scope) : IRequest<ForecastReportResponse>;

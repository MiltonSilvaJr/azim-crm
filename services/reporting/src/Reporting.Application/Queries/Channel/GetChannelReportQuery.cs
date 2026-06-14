using MediatR;
using Reporting.Contracts.Responses;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Queries.Channel;

/// <summary>
/// Query para o relatório de oportunidades por canal (Req 3).
///
/// Percentuais em basis points inteiros (base 10.000 = 100%) — sem float (DD-010, PBT-04).
///
/// Mapeia: TASK-09, design §5.2, Req 3, DD-010.
/// </summary>
public sealed record GetChannelReportQuery(
    Period Period,
    IEnumerable<Guid>? BuIds,
    ReportScope Scope) : IRequest<ChannelReportResponse>;

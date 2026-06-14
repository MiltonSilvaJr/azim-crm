using MediatR;
using Reporting.Application.Behaviors;
using Reporting.Contracts.Responses;
using Reporting.Domain.Enums;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Queries.Export;

/// <summary>
/// Query de export CSV para qualquer tipo de relatório (Req 5).
///
/// O handler reutiliza exatamente a query do relatório correspondente (mesmas linhas, mesmo escopo),
/// garantindo PBT-05 (round-trip relatório × CSV) e a "única fonte de verdade" (design §5.2).
///
/// Implementa <see cref="IScopedQuery"/> para validação de escopo no <c>AuthorizationBehavior</c>
/// sem chamada dupla ao <c>IScopeResolver</c> (dívida Onda 5).
///
/// Mapeia: TASK-11, Onda 6 (refactor), design §5.1, §5.2, §6.5, DD-004, Req 5, PBT-05, RNF 4, ADR-0001.
/// </summary>
public sealed record GetExportReportCsvQuery(
    ReportType ReportType,
    Period Period,
    IEnumerable<Guid>? BuIds,
    ReportScope Scope) : IRequest<CsvExportResponse>, IScopedQuery;

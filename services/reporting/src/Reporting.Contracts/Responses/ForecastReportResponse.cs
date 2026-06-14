using Reporting.Contracts.ReadModels;

namespace Reporting.Contracts.Responses;

/// <summary>
/// Resposta do relatório de forecast por BU/mês (Req 6).
/// Mapeia: TASK-07, design §5.2.
/// </summary>
public sealed record ForecastReportResponse(IReadOnlyList<ForecastRow> Rows);

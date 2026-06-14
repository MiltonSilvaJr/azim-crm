using Reporting.Contracts.ReadModels;

namespace Reporting.Contracts.Responses;

/// <summary>
/// Resposta do relatório de ranking por responsável (Req 2).
/// Linhas ordenadas por <c>WonValueCents</c> decrescente (Req 2.2).
/// Mapeia: TASK-08, design §5.2.
/// </summary>
public sealed record RankingReportResponse(IReadOnlyList<RankingRow> Rows);

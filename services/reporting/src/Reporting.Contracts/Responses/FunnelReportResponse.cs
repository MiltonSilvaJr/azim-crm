using Reporting.Contracts.ReadModels;

namespace Reporting.Contracts.Responses;

/// <summary>
/// Resposta do relatório de funil por estágio (Req 1).
/// Mapeia: TASK-06, design §5.2.
/// </summary>
public sealed record FunnelReportResponse(IReadOnlyList<FunnelRow> Stages);

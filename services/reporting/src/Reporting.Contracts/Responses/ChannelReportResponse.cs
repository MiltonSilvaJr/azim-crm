using Reporting.Contracts.ReadModels;

namespace Reporting.Contracts.Responses;

/// <summary>
/// Resposta do relatório de oportunidades por canal (Req 3).
/// Soma de <see cref="ChannelRow.PercentBasisPoints"/> = 10.000 para conjunto não-vazio (PBT-04).
/// Mapeia: TASK-09, design §5.2.
/// </summary>
public sealed record ChannelReportResponse(IReadOnlyList<ChannelRow> Rows);

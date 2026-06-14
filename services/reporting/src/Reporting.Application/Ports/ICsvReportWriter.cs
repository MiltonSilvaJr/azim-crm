using Reporting.Contracts.Responses;

namespace Reporting.Application.Ports;

/// <summary>
/// Porta de serialização de relatório para CSV.
///
/// A implementação é pura (sem IO) e vive na própria camada Application.
/// Responsabilidades: UTF-8 com BOM, cabeçalhos em pt-BR, valores monetários em R$, sem PII desnecessária.
///
/// Mapeia: TASK-05, TASK-11, design §5.2, §6.5, DD-004, DD-008, Req 5, RNF 4.
/// </summary>
public interface ICsvReportWriter
{
    /// <summary>
    /// Serializa um <see cref="FunnelReportResponse"/> para bytes CSV (UTF-8 com BOM).
    /// </summary>
    byte[] WriteFunnel(FunnelReportResponse response);

    /// <summary>
    /// Serializa um <see cref="ForecastReportResponse"/> para bytes CSV (UTF-8 com BOM).
    /// </summary>
    byte[] WriteForecast(ForecastReportResponse response);

    /// <summary>
    /// Serializa um <see cref="RankingReportResponse"/> para bytes CSV (UTF-8 com BOM).
    /// <see cref="RankingRow.DisplayName"/> incluído apenas quando presente na resposta (controlado pela <c>PiiMinimizationPolicy</c>).
    /// </summary>
    byte[] WriteRanking(RankingReportResponse response);

    /// <summary>
    /// Serializa um <see cref="ChannelReportResponse"/> para bytes CSV (UTF-8 com BOM).
    /// </summary>
    byte[] WriteChannel(ChannelReportResponse response);

    /// <summary>
    /// Serializa um <see cref="CommissionReportResponse"/> para bytes CSV (UTF-8 com BOM).
    /// </summary>
    byte[] WriteCommissions(CommissionReportResponse response);
}

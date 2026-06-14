using System.Text;
using Reporting.Application.Ports;
using Reporting.Contracts.Responses;

namespace Reporting.Application.Queries.Export;

/// <summary>
/// Implementação pura (sem IO de rede/disco) do <see cref="ICsvReportWriter"/>.
///
/// Responsabilidades:
/// <list type="bullet">
///   <item><description>UTF-8 com BOM (<c>\xEF\xBB\xBF</c>) obrigatório (Req 5.2).</description></item>
///   <item><description>Cabeçalhos em português brasileiro (Req 5.2).</description></item>
///   <item><description>Valores monetários formatados como <c>R$ X,XX</c> (apresentação apenas — DD-007).</description></item>
///   <item><description><c>DisplayName</c> incluído apenas no ranking e apenas quando não-null (DD-008, RNF 4).</description></item>
///   <item><description>Sem IO de rede — produz <c>byte[]</c> em memória.</description></item>
/// </list>
///
/// Mapeia: TASK-11, design §5.2, §6.5, DD-004, DD-007, DD-008, Req 5, RNF 4, PBT-05.
/// </summary>
public sealed class CsvReportWriter : ICsvReportWriter
{
    private static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    /// <inheritdoc/>
    public byte[] WriteFunnel(FunnelReportResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return Build(writer =>
        {
            writer.WriteLine("Estágio,Categoria,Quantidade,Total (R$),Forecast Ponderado (R$)");
            foreach (var row in response.Stages)
            {
                writer.WriteLine(
                    $"{EscapeCsv(row.StageName)},{EscapeCsv(row.Category)},{row.Count}," +
                    $"{FormatMoney(row.TotalCents)},{FormatMoney(row.WeightedForecastCents)}");
            }
        });
    }

    /// <inheritdoc/>
    public byte[] WriteForecast(ForecastReportResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return Build(writer =>
        {
            writer.WriteLine("BU,Ano,Mês,Forecast Ponderado (R$),Realizado (R$),Meta (R$)");
            foreach (var row in response.Rows)
            {
                var goalStr = row.GoalCents.HasValue ? FormatMoney(row.GoalCents.Value) : "";
                writer.WriteLine(
                    $"{EscapeCsv(row.BuName)},{row.Year},{row.Month}," +
                    $"{FormatMoney(row.WeightedForecastCents)},{FormatMoney(row.RealizedCents)},{goalStr}");
            }
        });
    }

    /// <inheritdoc/>
    public byte[] WriteRanking(RankingReportResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var hasDisplayName = response.Rows.Any(r => r.DisplayName != null);
        return Build(writer =>
        {
            if (hasDisplayName)
            {
                writer.WriteLine("ID Responsável,Nome,Ganhos (qtd),Valor Ganho (R$),Pipeline Forecast (R$)");
            }
            else
            {
                writer.WriteLine("ID Responsável,Ganhos (qtd),Valor Ganho (R$),Pipeline Forecast (R$)");
            }

            foreach (var row in response.Rows)
            {
                if (hasDisplayName)
                {
                    writer.WriteLine(
                        $"{row.OwnerId},{EscapeCsv(row.DisplayName ?? "")},{row.WonCount}," +
                        $"{FormatMoney(row.WonValueCents)},{FormatMoney(row.PipelineForecastCents)}");
                }
                else
                {
                    writer.WriteLine(
                        $"{row.OwnerId},{row.WonCount}," +
                        $"{FormatMoney(row.WonValueCents)},{FormatMoney(row.PipelineForecastCents)}");
                }
            }
        });
    }

    /// <inheritdoc/>
    public byte[] WriteChannel(ChannelReportResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return Build(writer =>
        {
            writer.WriteLine("Canal,Quantidade,Total (R$),Participação (%)");
            foreach (var row in response.Rows)
            {
                var pct = (row.PercentBasisPoints / 100.0m).ToString("F2");
                writer.WriteLine(
                    $"{EscapeCsv(row.ChannelName)},{row.Count},{FormatMoney(row.TotalCents)},{pct}%");
            }
        });
    }

    /// <inheritdoc/>
    public byte[] WriteCommissions(CommissionReportResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return Build(writer =>
        {
            writer.WriteLine("Parceiro,Comissão Projetada (R$),Comissão Consolidada (R$),Oportunidades");
            foreach (var row in response.Rows)
            {
                writer.WriteLine(
                    $"{EscapeCsv(row.PartnerName)},{FormatMoney(row.ProjectedCents)}," +
                    $"{FormatMoney(row.ConsolidatedCents)},{row.OpportunityCount}");
            }
        });
    }

    /// <summary>
    /// Formata centavos como <c>R$ X,XX</c> para apresentação no CSV.
    /// A conversão de centavos para reais ocorre APENAS aqui — na borda de apresentação (DD-007).
    /// </summary>
    public static string FormatMoney(long cents)
    {
        var reais = cents / 100m;
        return $"R$ {reais:F2}".Replace('.', ',');
    }

    /// <summary>
    /// Constrói o <c>byte[]</c> CSV com BOM UTF-8 usando <see cref="StreamWriter"/>.
    /// O <see cref="StreamWriter"/> com <see cref="Utf8WithBom"/> emite o BOM automaticamente.
    /// </summary>
    private static byte[] Build(Action<StreamWriter> writeContent)
    {
        using var ms     = new MemoryStream();
        using var writer = new StreamWriter(ms, Utf8WithBom, leaveOpen: true);
        writeContent(writer);
        writer.Flush();
        return ms.ToArray();
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}

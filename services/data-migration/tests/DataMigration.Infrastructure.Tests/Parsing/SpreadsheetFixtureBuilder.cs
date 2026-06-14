using ClosedXML.Excel;

namespace DataMigration.Infrastructure.Tests.Parsing;

/// <summary>
/// Utilitário que gera arquivos .xlsx em memória para testes.
///
/// Simula a estrutura da planilha Pipeline Vellus.xlsx com:
/// - Aba "Pipeline" com colunas obrigatórias.
/// - Aba "Ações Comerciais" com colunas obrigatórias.
///
/// Rastreia: design §6.4, TASK-14, ST-01.
/// </summary>
internal static class SpreadsheetFixtureBuilder
{
    /// <summary>
    /// Cria um arquivo .xlsx em memória com os cabeçalhos completos e as linhas de dados fornecidas.
    /// </summary>
    public static Stream BuildWithData(
        IReadOnlyList<PipelineRowData>? pipelineRows = null,
        IReadOnlyList<AcoesRowData>? acoesRows = null)
    {
        using var workbook = new XLWorkbook();

        // Aba Pipeline.
        var pipeline = workbook.Worksheets.Add("Pipeline");
        AddPipelineHeaders(pipeline);

        var dataRows = pipelineRows ?? Array.Empty<PipelineRowData>();
        for (var i = 0; i < dataRows.Count; i++)
        {
            AddPipelineRow(pipeline, dataRows[i], i + 2);
        }

        // Aba Ações Comerciais.
        var acoes = workbook.Worksheets.Add("Ações Comerciais");
        AddAcoesHeaders(acoes);

        var activityRows = acoesRows ?? Array.Empty<AcoesRowData>();
        for (var i = 0; i < activityRows.Count; i++)
        {
            AddAcoesRow(acoes, activityRows[i], i + 2);
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    /// <summary>Cria planilha somente com cabeçalhos (sem dados).</summary>
    public static Stream BuildEmpty()
        => BuildWithData(Array.Empty<PipelineRowData>(), Array.Empty<AcoesRowData>());

    private static void AddPipelineHeaders(IXLWorksheet sheet)
    {
        sheet.Cell(1, 1).Value = "BU";
        sheet.Cell(1, 2).Value = "Conta";
        sheet.Cell(1, 3).Value = "Responsável";
        sheet.Cell(1, 4).Value = "Título da Oportunidade";
        sheet.Cell(1, 5).Value = "Etapa";
        sheet.Cell(1, 6).Value = "Nº";
        sheet.Cell(1, 7).Value = "Valor Setup (R$)";
        sheet.Cell(1, 8).Value = "Valor Mensal (R$)";
        sheet.Cell(1, 9).Value = "Meses";
        sheet.Cell(1, 10).Value = "Probabilidade (%)";
        sheet.Cell(1, 11).Value = "Forecast (R$)";
        sheet.Cell(1, 12).Value = "Parceiro";
        sheet.Cell(1, 13).Value = "Data de Fechamento";
    }

    private static void AddPipelineRow(IXLWorksheet sheet, PipelineRowData row, int rowNum)
    {
        sheet.Cell(rowNum, 1).Value = row.Bu;
        sheet.Cell(rowNum, 2).Value = row.Conta;
        sheet.Cell(rowNum, 3).Value = row.Responsavel;
        sheet.Cell(rowNum, 4).Value = row.TituloOportunidade;
        sheet.Cell(rowNum, 5).Value = row.Etapa;
        sheet.Cell(rowNum, 6).Value = row.Numero;
        sheet.Cell(rowNum, 7).Value = row.ValorSetup;
        sheet.Cell(rowNum, 8).Value = row.ValorMensal;
        sheet.Cell(rowNum, 9).Value = row.Meses;
        sheet.Cell(rowNum, 10).Value = row.Probabilidade;
        sheet.Cell(rowNum, 11).Value = row.Forecast;
        sheet.Cell(rowNum, 12).Value = row.Parceiro;

        if (row.DataFechamentoSerial.HasValue)
        {
            // Grava o serial numérico diretamente para testar a conversão.
            sheet.Cell(rowNum, 13).Value = row.DataFechamentoSerial.Value;
        }
    }

    private static void AddAcoesHeaders(IXLWorksheet sheet)
    {
        sheet.Cell(1, 1).Value = "Nº Oportunidade";
        sheet.Cell(1, 2).Value = "Descrição";
        sheet.Cell(1, 3).Value = "Data";
        sheet.Cell(1, 4).Value = "Tipo";
    }

    private static void AddAcoesRow(IXLWorksheet sheet, AcoesRowData row, int rowNum)
    {
        sheet.Cell(rowNum, 1).Value = row.NumeroOportunidade;
        sheet.Cell(rowNum, 2).Value = row.Descricao;
        if (row.DataSerial.HasValue)
        {
            sheet.Cell(rowNum, 3).Value = row.DataSerial.Value;
        }
        else
        {
            sheet.Cell(rowNum, 3).Value = string.Empty;
        }
        sheet.Cell(rowNum, 4).Value = row.Tipo;
    }
}

/// <summary>Dados de uma linha da aba Pipeline para fixture.</summary>
internal sealed record PipelineRowData(
    string Bu = "Sertão",
    string Conta = "Acme Corp",
    string Responsavel = "Milton",
    string TituloOportunidade = "Oportunidade Teste",
    string Etapa = "Proposta",
    string Numero = "AZ-0095",
    double ValorSetup = 100.00,
    double ValorMensal = 50.00,
    int Meses = 12,
    int Probabilidade = 70,
    double Forecast = 420.00,
    string Parceiro = "-",
    int? DataFechamentoSerial = null);

/// <summary>Dados de uma linha da aba Ações Comerciais para fixture.</summary>
internal sealed record AcoesRowData(
    string NumeroOportunidade = "AZ-0095",
    string Descricao = "Reunião de apresentação",
    int? DataSerial = null,
    string Tipo = "Reunião");

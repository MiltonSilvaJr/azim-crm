using ClosedXML.Excel;
using DataMigration.Application.Ports;
using DataMigration.Domain.ValueObjects;

namespace DataMigration.Infrastructure.Parsing;

/// <summary>
/// Adaptador de parsing de planilha .xlsx usando ClosedXML (DD-002).
///
/// Confinado à Infrastructure — o Domain nunca referencia ClosedXML
/// (validado por DataMigration.Architecture.Tests).
///
/// Implementa <see cref="ISpreadsheetParser"/> (definida em Application).
///
/// Rastreia: design §6.4, DD-002, Req 1, Req 3, Req 9, Req 10, TASK-14.
/// </summary>
internal sealed class SpreadsheetParser : ISpreadsheetParser
{
    private const string PipelineSheetName = "Pipeline";
    private const string AcoesSheetName = "Ações Comerciais";

    /// <inheritdoc />
    public Task<SpreadsheetStructure> ParseStructureAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var workbook = new XLWorkbook(stream);

        var pipelineCols = ReadColumnHeaders(workbook, PipelineSheetName);
        var acoesCols = ReadColumnHeaders(workbook, AcoesSheetName);
        var rowCount = CountDataRows(workbook, PipelineSheetName);

        var structure = new SpreadsheetStructure(
            PipelineColumns: pipelineCols,
            AcoesColumns: acoesCols,
            DetectedRowCount: rowCount);

        return Task.FromResult(structure);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SourceRow>> ParseRowsAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var workbook = new XLWorkbook(stream);

        var rows = new List<SourceRow>();

        rows.AddRange(ReadSheetRows(workbook, PipelineSheetName));
        rows.AddRange(ReadSheetRows(workbook, AcoesSheetName));

        return Task.FromResult<IReadOnlyList<SourceRow>>(rows.AsReadOnly());
    }

    // =========================================================================
    // Helpers privados
    // =========================================================================

    private static IReadOnlyList<string> ReadColumnHeaders(IXLWorkbook workbook, string sheetName)
    {
        if (!workbook.TryGetWorksheet(sheetName, out var sheet))
        {
            return Array.Empty<string>();
        }

        var headerRow = sheet.Row(1);
        var columns = new List<string>();

        foreach (var cell in headerRow.CellsUsed())
        {
            var value = cell.GetString().Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                columns.Add(value);
            }
        }

        return columns.AsReadOnly();
    }

    private static int CountDataRows(IXLWorkbook workbook, string sheetName)
    {
        if (!workbook.TryGetWorksheet(sheetName, out var sheet))
        {
            return 0;
        }

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;

        // Subtrai a linha de cabeçalho (linha 1).
        return Math.Max(0, lastRow - 1);
    }

    private static IEnumerable<SourceRow> ReadSheetRows(IXLWorkbook workbook, string sheetName)
    {
        if (!workbook.TryGetWorksheet(sheetName, out var sheet))
        {
            yield break;
        }

        // Lê cabeçalhos da linha 1.
        var headerRow = sheet.Row(1);
        var headers = new Dictionary<int, string>();

        foreach (var cell in headerRow.CellsUsed())
        {
            var header = cell.GetString().Trim();
            if (!string.IsNullOrWhiteSpace(header))
            {
                headers[cell.Address.ColumnNumber] = header;
            }
        }

        if (headers.Count == 0)
        {
            yield break;
        }

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;

        // Itera as linhas de dados (a partir da linha 2).
        for (var rowNum = 2; rowNum <= lastRow; rowNum++)
        {
            var row = sheet.Row(rowNum);

            // Ignora linha completamente vazia.
            if (row.IsEmpty())
            {
                continue;
            }

            var cells = new Dictionary<string, string?>();

            foreach (var (colNum, colName) in headers)
            {
                var cell = row.Cell(colNum);
                var rawValue = GetCellRawValue(cell);
                cells[colName] = rawValue;
            }

            // RowIndex base-0: linha de dados 2 → índice 0.
            yield return new SourceRow(
                sheetName: sheetName,
                rowIndex: rowNum - 2,
                cells: cells);
        }
    }

    /// <summary>
    /// Retorna o valor bruto da célula como string.
    ///
    /// Para células numéricas (incluindo seriais de data Excel), retorna o
    /// valor numérico sem conversão de tipo para que o <c>CanonicalRowMapper</c>
    /// possa aplicar a conversão correta via <c>ExcelSerialDate</c> (DD-006).
    /// </summary>
    private static string? GetCellRawValue(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return null;
        }

        // Se a célula é data ClosedXML detecta automaticamente — retorna o serial.
        if (cell.DataType == XLDataType.DateTime || cell.DataType == XLDataType.TimeSpan)
        {
            // Retorna o serial numérico Excel para uso do ExcelSerialDate (DD-006).
            try
            {
                var dateTime = cell.GetDateTime();
                var epoch = new DateTime(1899, 12, 30);
                var serial = (int)(dateTime - epoch).TotalDays;
                return serial.ToString();
            }
            catch
            {
                return cell.GetString();
            }
        }

        if (cell.DataType == XLDataType.Number)
        {
            // Retorna o número sem formatação para preservar precisão.
            return cell.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return cell.GetString();
    }
}

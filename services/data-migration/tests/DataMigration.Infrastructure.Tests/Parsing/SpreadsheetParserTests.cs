using DataMigration.Domain.ValueObjects;
using DataMigration.Infrastructure.Parsing;
using FluentAssertions;
using Xunit;

namespace DataMigration.Infrastructure.Tests.Parsing;

/// <summary>
/// Testes de integração do <see cref="SpreadsheetParser"/> (ClosedXML).
///
/// Rastreia: TASK-14, ST-01; design §6.4, DD-002.
/// </summary>
public sealed class SpreadsheetParserTests
{
    private readonly SpreadsheetParser _sut = new();

    // =========================================================================
    // ParseStructureAsync
    // =========================================================================

    [Fact]
    public async Task ParseStructure_QuandoPlanilhaValida_RetornaColunasEContagem()
    {
        // Arrange
        var stream = SpreadsheetFixtureBuilder.BuildWithData(
            pipelineRows: new[] { new PipelineRowData() },
            acoesRows: new[] { new AcoesRowData() });

        // Act
        var structure = await _sut.ParseStructureAsync(stream);

        // Assert
        structure.PipelineColumns.Should().Contain("BU");
        structure.PipelineColumns.Should().Contain("Conta");
        structure.PipelineColumns.Should().Contain("Responsável");
        structure.PipelineColumns.Should().Contain("Nº");
        structure.AcoesColumns.Should().Contain("Nº Oportunidade");
        structure.AcoesColumns.Should().Contain("Tipo");
        structure.DetectedRowCount.Should().Be(1);
    }

    [Fact]
    public async Task ParseStructure_QuandoPlanilhaVazia_RetornaContagemZero()
    {
        // Arrange
        var stream = SpreadsheetFixtureBuilder.BuildEmpty();

        // Act
        var structure = await _sut.ParseStructureAsync(stream);

        // Assert
        structure.DetectedRowCount.Should().Be(0);
    }

    // =========================================================================
    // ParseRowsAsync — aba Pipeline
    // =========================================================================

    [Fact]
    public async Task ParseRows_QuandoUmaLinhaPipeline_RetornaSourceRowPipeline()
    {
        // Arrange
        var stream = SpreadsheetFixtureBuilder.BuildWithData(
            pipelineRows: new[] { new PipelineRowData(Bu: "Sertão ", Conta: "Acme Corp") },
            acoesRows: Array.Empty<AcoesRowData>());

        // Act
        var rows = await _sut.ParseRowsAsync(stream);

        // Assert
        rows.Should().HaveCount(1);
        rows[0].SheetName.Should().Be("Pipeline");
        rows[0].RowIndex.Should().Be(0);
        rows[0].Cells["BU"].Should().Be("Sertão "); // raw — trim feito pelo CanonicalRowMapper
        rows[0].Cells["Conta"].Should().Be("Acme Corp");
    }

    [Fact]
    public async Task ParseRows_QuandoDuasAbasComDados_RetornaLinhasDasAbasCombinadas()
    {
        // Arrange
        var stream = SpreadsheetFixtureBuilder.BuildWithData(
            pipelineRows: new[] { new PipelineRowData(), new PipelineRowData(Conta: "Beta Ltda") },
            acoesRows: new[] { new AcoesRowData() });

        // Act
        var rows = await _sut.ParseRowsAsync(stream);

        // Assert — 2 pipeline + 1 acoes = 3 linhas
        rows.Should().HaveCount(3);
        rows.Count(r => r.SheetName == "Pipeline").Should().Be(2);
        rows.Count(r => r.SheetName == "Ações Comerciais").Should().Be(1);
    }

    [Fact]
    public async Task ParseRows_QuandoSerialExcel_RetornaValorNumericoNaCelula()
    {
        // Arrange — serial 45000 corresponde a uma data ISO
        var stream = SpreadsheetFixtureBuilder.BuildWithData(
            pipelineRows: new[] { new PipelineRowData(DataFechamentoSerial: 45000) },
            acoesRows: Array.Empty<AcoesRowData>());

        // Act
        var rows = await _sut.ParseRowsAsync(stream);

        // Assert — a célula de data de fechamento deve conter o serial como string
        rows.Should().HaveCount(1);
        var dataFechamento = rows[0].Cells[ColumnNames.DataFechamento];
        dataFechamento.Should().NotBeNullOrEmpty("o serial deve estar presente para conversão pelo CanonicalRowMapper");
    }

    [Fact]
    public async Task ParseRows_QuandoMoneyVazio_RetornaNullNaCelula()
    {
        // Arrange — row sem valor de setup
        var pipelineRow = new PipelineRowData(ValorSetup: 0.0);
        var stream = SpreadsheetFixtureBuilder.BuildWithData(
            pipelineRows: new[] { pipelineRow },
            acoesRows: Array.Empty<AcoesRowData>());

        // Act
        var rows = await _sut.ParseRowsAsync(stream);

        // Assert — valor numérico 0 presente na célula
        rows.Should().HaveCount(1);
        rows[0].Cells.Should().ContainKey(ColumnNames.ValorSetup);
    }

    [Fact]
    public async Task ParseRows_ParceiroHifen_RetornaCelulaComHifen()
    {
        // Arrange
        var stream = SpreadsheetFixtureBuilder.BuildWithData(
            pipelineRows: new[] { new PipelineRowData(Parceiro: "-") },
            acoesRows: Array.Empty<AcoesRowData>());

        // Act
        var rows = await _sut.ParseRowsAsync(stream);

        // Assert — "-" raw; CanonicalRowMapper converte para sem parceiro
        rows[0].Cells[ColumnNames.Parceiro].Should().Be("-");
    }
}

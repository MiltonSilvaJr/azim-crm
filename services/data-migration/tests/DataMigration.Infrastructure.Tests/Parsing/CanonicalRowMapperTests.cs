using DataMigration.Domain.ValueObjects;
using DataMigration.Infrastructure.Parsing;
using FluentAssertions;
using Xunit;

namespace DataMigration.Infrastructure.Tests.Parsing;

/// <summary>
/// Testes unitários do <see cref="CanonicalRowMapper"/>.
///
/// Rastreia: TASK-14, ST-01; design §5.5, Req 3.
/// </summary>
public sealed class CanonicalRowMapperTests
{
    private readonly CanonicalRowMapper _sut = new();

    // =========================================================================
    // MapPipelineRow — transformações de Req 3
    // =========================================================================

    [Fact]
    public void MapPipeline_QuandoBuComEspaco_AplicaTrim()
    {
        // Arrange
        var row = BuildPipelineRow(bu: "Sertão ");

        // Act
        var result = _sut.MapPipelineRow(row);

        // Assert
        result.Should().NotBeNull();
        result!.Bu.Should().Be("Sertão");
    }

    [Fact]
    public void MapPipeline_QuandoContaComNome_NormalizaParaDedupe()
    {
        // Arrange
        var row = BuildPipelineRow(conta: "Pag.ai");

        // Act
        var result = _sut.MapPipelineRow(row);

        // Assert
        result!.AccountName.Value.Should().Be("pag.ai"); // lowercase
    }

    [Fact]
    public void MapPipeline_QuandoTituloVazio_UsaFallbackComConta()
    {
        // Arrange — título vazio, conta = "Acme Corp" (Req 3.3)
        var row = BuildPipelineRow(titulo: string.Empty, conta: "Acme Corp");

        // Act
        var result = _sut.MapPipelineRow(row);

        // Assert
        result!.Title.Should().Be("Oportunidade — acme corp");
    }

    [Fact]
    public void MapPipeline_QuandoTituloPreenchido_MantemTitulo()
    {
        // Arrange
        var row = BuildPipelineRow(titulo: "Proposta Comercial");

        // Act
        var result = _sut.MapPipelineRow(row);

        // Assert
        result!.Title.Should().Be("Proposta Comercial");
    }

    [Fact]
    public void MapPipeline_QuandoParceiroHifen_RetornaPartnerNameNulo()
    {
        // Arrange (Req 3.4)
        var row = BuildPipelineRow(parceiro: "-");

        // Act
        var result = _sut.MapPipelineRow(row);

        // Assert
        result!.PartnerName.Should().BeNull();
    }

    [Fact]
    public void MapPipeline_QuandoParceiroVazio_RetornaPartnerNameNulo()
    {
        // Arrange
        var row = BuildPipelineRow(parceiro: string.Empty);

        // Act
        var result = _sut.MapPipelineRow(row);

        // Assert
        result!.PartnerName.Should().BeNull();
    }

    [Fact]
    public void MapPipeline_QuandoParceiroPreenchido_RetornaPartnerName()
    {
        // Arrange
        var row = BuildPipelineRow(parceiro: "Salesforce");

        // Act
        var result = _sut.MapPipelineRow(row);

        // Assert
        result!.PartnerName.Should().Be("Salesforce");
    }

    [Fact]
    public void MapPipeline_QuandoMoneyPreenchido_ConverteCentavos()
    {
        // Arrange — R$ 1.500,50 = 150050 centavos (Req 3.6)
        var row = BuildPipelineRow(valorSetup: "1500.50", valorMensal: "500.00");

        // Act
        var result = _sut.MapPipelineRow(row);

        // Assert
        result!.ValorSetupCents.Should().Be(150050L);
        result.ValorMensalCents.Should().Be(50000L);
    }

    [Fact]
    public void MapPipeline_QuandoMoneyVazio_RetornaZero()
    {
        // Arrange (Req 3.6 — vazio → 0)
        var row = BuildPipelineRow(valorSetup: null);

        // Act
        var result = _sut.MapPipelineRow(row);

        // Assert
        result!.ValorSetupCents.Should().Be(0L);
    }

    [Fact]
    public void MapPipeline_QuandoSerialExcel_ConverteDateOnly()
    {
        // Arrange — serial 45000 = 2023-03-15 (epoch 1899-12-30, DD-006)
        var serial = 45000;
        var expectedDate = ExcelSerialDate.FromSerial(serial)!.Value;
        var row = BuildPipelineRow(dataFechamento: serial.ToString());

        // Act
        var result = _sut.MapPipelineRow(row);

        // Assert
        result!.CloseDate.Should().Be(expectedDate);
    }

    [Fact]
    public void MapPipeline_QuandoDataVazia_RetornaCloseDateNulo()
    {
        // Arrange
        var row = BuildPipelineRow(dataFechamento: null);

        // Act
        var result = _sut.MapPipelineRow(row);

        // Assert
        result!.CloseDate.Should().BeNull();
    }

    [Fact]
    public void MapPipeline_QuandoNumeroAzValido_PreservaNumero()
    {
        // Arrange
        var row = BuildPipelineRow(numero: "AZ-0043");

        // Act
        var result = _sut.MapPipelineRow(row);

        // Assert
        result!.PreservedNumber.Should().NotBeNull();
        result.PreservedNumber!.Value.Should().Be("AZ-0043");
    }

    [Fact]
    public void MapPipeline_QuandoNumeroInvalido_RetornaPreservedNumberNulo()
    {
        // Arrange
        var row = BuildPipelineRow(numero: "invalido");

        // Act
        var result = _sut.MapPipelineRow(row);

        // Assert
        result!.PreservedNumber.Should().BeNull();
    }

    [Fact]
    public void MapPipeline_QuandoAbaErrada_RetornaNull()
    {
        // Arrange
        var row = new SourceRow("Ações Comerciais", 0, new Dictionary<string, string?>());

        // Act
        var result = _sut.MapPipelineRow(row);

        // Assert
        result.Should().BeNull();
    }

    // =========================================================================
    // MapAcoesRow
    // =========================================================================

    [Fact]
    public void MapAcoes_QuandoAbaCorreta_RetornaLinhaAcoes()
    {
        // Arrange
        var row = BuildAcoesRow(numero: "AZ-0095", descricao: "Reunião inicial", tipo: "Reunião");

        // Act
        var result = _sut.MapAcoesRow(row);

        // Assert
        result.Should().NotBeNull();
        result!.OpportunityNumber.Should().NotBeNull();
        result.OpportunityNumber!.Value.Should().Be("AZ-0095");
        result.Description.Should().Be("Reunião inicial");
        result.Type.Should().Be("Reunião");
    }

    [Fact]
    public void MapAcoes_QuandoNumeroInvalido_RetornaOpportunityNumberNulo()
    {
        // Arrange
        var row = BuildAcoesRow(numero: "invalido");

        // Act
        var result = _sut.MapAcoesRow(row);

        // Assert
        result!.OpportunityNumber.Should().BeNull();
    }

    [Fact]
    public void MapAcoes_QuandoAbaErrada_RetornaNull()
    {
        // Arrange
        var row = new SourceRow("Pipeline", 0, new Dictionary<string, string?>());

        // Act
        var result = _sut.MapAcoesRow(row);

        // Assert
        result.Should().BeNull();
    }

    // =========================================================================
    // ParseCents — helper interno (testado via método internal)
    // =========================================================================

    [Theory]
    [InlineData("1500.50", 150050L)]
    [InlineData("500", 50000L)]
    [InlineData("0", 0L)]
    [InlineData("", 0L)]
    [InlineData(null, 0L)]
    [InlineData("1.234,56", 123456L)]  // formato BR com separador de milhar
    public void ParseCents_Casos_Variados(string? input, long esperado)
    {
        // Act
        var result = CanonicalRowMapper.ParseCents(input);

        // Assert
        result.Should().Be(esperado);
    }

    // =========================================================================
    // Builders de SourceRow para testes
    // =========================================================================

    private static SourceRow BuildPipelineRow(
        string bu = "Sertão",
        string conta = "Acme Corp",
        string? responsavel = "Milton",
        string titulo = "Oportunidade Teste",
        string etapa = "Proposta",
        string? numero = "AZ-0095",
        string? valorSetup = "100.00",
        string? valorMensal = "50.00",
        string? meses = "12",
        string? probabilidade = "70",
        string? forecast = "420.00",
        string? parceiro = "-",
        string? dataFechamento = null)
    {
        var cells = new Dictionary<string, string?>
        {
            [ColumnNames.Bu] = bu,
            [ColumnNames.Conta] = conta,
            [ColumnNames.Responsavel] = responsavel,
            [ColumnNames.TituloOportunidade] = titulo,
            [ColumnNames.Etapa] = etapa,
            [ColumnNames.Numero] = numero,
            [ColumnNames.ValorSetup] = valorSetup,
            [ColumnNames.ValorMensal] = valorMensal,
            [ColumnNames.Meses] = meses,
            [ColumnNames.Probabilidade] = probabilidade,
            [ColumnNames.ForecastPonderado] = forecast,
            [ColumnNames.Parceiro] = parceiro,
            [ColumnNames.DataFechamento] = dataFechamento,
        };

        return new SourceRow("Pipeline", 0, cells);
    }

    private static SourceRow BuildAcoesRow(
        string numero = "AZ-0095",
        string descricao = "Descrição",
        string? data = null,
        string tipo = "Reunião")
    {
        var cells = new Dictionary<string, string?>
        {
            [ColumnNames.NumeroOportunidade] = numero,
            [ColumnNames.Descricao] = descricao,
            [ColumnNames.DataAtividade] = data,
            [ColumnNames.TipoAtividade] = tipo,
        };

        return new SourceRow("Ações Comerciais", 0, cells);
    }
}

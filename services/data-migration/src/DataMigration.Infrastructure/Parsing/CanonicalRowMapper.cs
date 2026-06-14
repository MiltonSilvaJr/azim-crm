using DataMigration.Domain.ValueObjects;

namespace DataMigration.Infrastructure.Parsing;

/// <summary>
/// Resultado do mapeamento canônico de uma linha da aba Pipeline.
///
/// Rastreia: design §5.5, Req 3, TASK-14.
/// </summary>
public sealed record MappedPipelineRow(
    /// <summary>Business Unit (trim aplicado, Req 3.1).</summary>
    string Bu,
    /// <summary>Nome da conta normalizado para dedupe.</summary>
    NormalizedName AccountName,
    /// <summary>Nome do responsável (owner).</summary>
    string? OwnerName,
    /// <summary>Título da oportunidade (ou "Oportunidade — {conta}" se vazio).</summary>
    string Title,
    /// <summary>Etapa do funil de vendas.</summary>
    string StageName,
    /// <summary>Número AZ-NNNN preservado (pode ser nulo se não constar na planilha).</summary>
    OpportunityNumber? PreservedNumber,
    /// <summary>Valor de setup em centavos (money-as-cents).</summary>
    long ValorSetupCents,
    /// <summary>Valor mensal em centavos (money-as-cents).</summary>
    long ValorMensalCents,
    /// <summary>Quantidade de meses do contrato.</summary>
    int Meses,
    /// <summary>Probabilidade de fechamento (0-100).</summary>
    int Probabilidade,
    /// <summary>Forecast ponderado da planilha em centavos.</summary>
    long ForecastPlanilhaCents,
    /// <summary>Nome do parceiro (nulo quando "-" ou vazio, Req 3.4).</summary>
    string? PartnerName,
    /// <summary>Data de fechamento convertida do serial Excel (DD-006).</summary>
    DateOnly? CloseDate,
    /// <summary>Índice original da linha na aba (base-0).</summary>
    int RowIndex);

/// <summary>
/// Resultado do mapeamento canônico de uma linha da aba Ações Comerciais.
///
/// Rastreia: design §5.5, Req 10, TASK-14.
/// </summary>
public sealed record MappedAcoesRow(
    /// <summary>Número AZ-NNNN da oportunidade relacionada.</summary>
    OpportunityNumber? OpportunityNumber,
    /// <summary>Descrição técnica da atividade (sem PII).</summary>
    string Description,
    /// <summary>Data da atividade convertida do serial Excel.</summary>
    DateOnly? ActivityDate,
    /// <summary>Tipo de atividade.</summary>
    string Type,
    /// <summary>Índice original da linha na aba (base-0).</summary>
    int RowIndex);

/// <summary>
/// Transforma <see cref="SourceRow"/> nas estruturas canônicas de mapeamento,
/// aplicando todas as transformações de Req 3.
///
/// Transformações aplicadas (design §5.5):
/// <list type="bullet">
///   <item>Trim de BU: "Sertão " → "Sertão" (Req 3.1).</item>
///   <item>Normalização de nome de conta para dedupe (Req 7).</item>
///   <item>Título vazio → "Oportunidade — {conta}" (Req 3.3).</item>
///   <item>"-" em Parceiro → sem parceiro (Req 3.4).</item>
///   <item>Valores monetários vazios → 0 em centavos (Req 3.6).</item>
///   <item>Conversão de serial Excel para <see cref="DateOnly"/> (DD-006).</item>
/// </list>
///
/// Rastreia: design §5.5, Req 3, DD-006, TASK-14.
/// </summary>
public sealed class CanonicalRowMapper
{
    private const string PipelineSheet = "Pipeline";

    /// <summary>
    /// Mapeia uma <see cref="SourceRow"/> da aba Pipeline.
    /// Retorna <c>null</c> se a linha não pertencer à aba Pipeline.
    /// </summary>
    public MappedPipelineRow? MapPipelineRow(SourceRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!string.Equals(row.SheetName, PipelineSheet, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var cells = row.Cells;

        // BU com trim (Req 3.1).
        var bu = (cells.GetValueOrDefault(ColumnNames.Bu) ?? string.Empty).Trim();

        // Nome de conta normalizado.
        var contaRaw = (cells.GetValueOrDefault(ColumnNames.Conta) ?? string.Empty).Trim();
        var accountName = NormalizedName.From(string.IsNullOrWhiteSpace(contaRaw) ? "sem-conta" : contaRaw);

        // Owner.
        var ownerName = cells.GetValueOrDefault(ColumnNames.Responsavel)?.Trim();
        if (string.IsNullOrWhiteSpace(ownerName))
        {
            ownerName = null;
        }

        // Título (Req 3.3): vazio → "Oportunidade — {conta}".
        var titleRaw = cells.GetValueOrDefault(ColumnNames.TituloOportunidade)?.Trim();
        var title = string.IsNullOrWhiteSpace(titleRaw)
            ? $"Oportunidade — {accountName.Value}"
            : titleRaw;

        // Etapa.
        var stageName = (cells.GetValueOrDefault(ColumnNames.Etapa) ?? string.Empty).Trim();

        // Número AZ-NNNN preservado (nulo quando não constar ou formato inválido).
        var numeroRaw = cells.GetValueOrDefault(ColumnNames.Numero)?.Trim();
        OpportunityNumber? preservedNumber = null;
        if (!string.IsNullOrWhiteSpace(numeroRaw))
        {
            preservedNumber = OpportunityNumber.Parse(numeroRaw);
        }

        // Valores monetários em centavos (Req 3.6, money-as-cents).
        var valorSetupCents = ParseCents(cells.GetValueOrDefault(ColumnNames.ValorSetup));
        var valorMensalCents = ParseCents(cells.GetValueOrDefault(ColumnNames.ValorMensal));
        var meses = ParseInt(cells.GetValueOrDefault(ColumnNames.Meses));
        var probabilidade = ParseInt(cells.GetValueOrDefault(ColumnNames.Probabilidade));
        var forecastCents = ParseCents(cells.GetValueOrDefault(ColumnNames.ForecastPonderado));

        // Parceiro: "-" ou vazio → sem parceiro (Req 3.4).
        var parceiroRaw = cells.GetValueOrDefault(ColumnNames.Parceiro)?.Trim();
        var partnerName = string.IsNullOrWhiteSpace(parceiroRaw) || parceiroRaw == "-"
            ? null
            : parceiroRaw;

        // Data de fechamento via ExcelSerialDate (DD-006).
        var dataRaw = cells.GetValueOrDefault(ColumnNames.DataFechamento);
        var closeDate = ParseExcelDate(dataRaw);

        return new MappedPipelineRow(
            Bu: bu,
            AccountName: accountName,
            OwnerName: ownerName,
            Title: title,
            StageName: stageName,
            PreservedNumber: preservedNumber,
            ValorSetupCents: valorSetupCents,
            ValorMensalCents: valorMensalCents,
            Meses: meses,
            Probabilidade: probabilidade,
            ForecastPlanilhaCents: forecastCents,
            PartnerName: partnerName,
            CloseDate: closeDate,
            RowIndex: row.RowIndex);
    }

    /// <summary>
    /// Mapeia uma <see cref="SourceRow"/> da aba Ações Comerciais.
    /// Retorna <c>null</c> se a linha não pertencer a essa aba.
    /// </summary>
    public MappedAcoesRow? MapAcoesRow(SourceRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!string.Equals(row.SheetName, "Ações Comerciais", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var cells = row.Cells;

        // Número da oportunidade relacionada.
        var numRaw = cells.GetValueOrDefault(ColumnNames.NumeroOportunidade)?.Trim();
        OpportunityNumber? oppNumber = null;
        if (!string.IsNullOrWhiteSpace(numRaw) && OpportunityNumber.IsValid(numRaw))
        {
            oppNumber = OpportunityNumber.Parse(numRaw);
        }

        var description = (cells.GetValueOrDefault(ColumnNames.Descricao) ?? string.Empty).Trim();
        var type = (cells.GetValueOrDefault(ColumnNames.TipoAtividade) ?? string.Empty).Trim();

        var dataRaw = cells.GetValueOrDefault(ColumnNames.DataAtividade);
        var activityDate = ParseExcelDate(dataRaw);

        return new MappedAcoesRow(
            OpportunityNumber: oppNumber,
            Description: description,
            ActivityDate: activityDate,
            Type: type,
            RowIndex: row.RowIndex);
    }

    // =========================================================================
    // Conversões privadas
    // =========================================================================

    /// <summary>
    /// Converte um valor monetário em reais (string) para centavos inteiros.
    /// Valor vazio ou inválido → 0 (Req 3.6).
    /// Proibido float/double — usa conversão inteira segura.
    /// </summary>
    internal static long ParseCents(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0L;
        }

        // Remove separadores de milhar e normaliza separador decimal.
        var cleaned = raw
            .Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty)
            .Trim();

        // Trata formatos com ponto como milhar e vírgula como decimal (padrão BR).
        // Ex: "1.234,56" → "1234.56"
        if (cleaned.Contains(','))
        {
            cleaned = cleaned.Replace(".", string.Empty).Replace(',', '.');
        }

        if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var reais))
        {
            // Converte para centavos usando Decimal para evitar erro de ponto flutuante.
            return (long)Math.Round(reais * 100m, MidpointRounding.AwayFromZero);
        }

        return 0L;
    }

    internal static int ParseInt(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        // Remove casas decimais (ex: "6.0" → 6).
        if (double.TryParse(raw, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d))
        {
            return (int)Math.Round(d);
        }

        return int.TryParse(raw.Trim(), out var i) ? i : 0;
    }

    internal static DateOnly? ParseExcelDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (int.TryParse(raw.Trim(), out var serial))
        {
            var date = ExcelSerialDate.FromSerial(serial);
            return date?.Value;
        }

        // Tenta parse direto de data ISO.
        if (DateOnly.TryParse(raw.Trim(), out var directDate))
        {
            return directDate;
        }

        return null;
    }
}

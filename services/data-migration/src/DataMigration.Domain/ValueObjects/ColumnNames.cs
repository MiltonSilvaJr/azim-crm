namespace DataMigration.Domain.ValueObjects;

/// <summary>
/// Constantes com os nomes canônicos das colunas esperadas na planilha
/// (aba "Pipeline" e aba "Ações Comerciais").
///
/// Usado pelo <c>CanonicalRowMapper</c> (Infrastructure) e pelo validador
/// de estrutura de colunas (<c>SpreadsheetStructureValidator</c>, Application).
///
/// Rastreia: design §4.3, §5.5, Req 1.2, Req 3, TASK-07.
/// </summary>
public static class ColumnNames
{
    // =========================================================================
    // Aba Pipeline (required)
    // =========================================================================

    /// <summary>Business Unit responsável pela oportunidade.</summary>
    public const string Bu = "BU";

    /// <summary>Nome da conta / empresa cliente.</summary>
    public const string Conta = "Conta";

    /// <summary>Nome do responsável (owner).</summary>
    public const string Responsavel = "Responsável";

    /// <summary>Título/nome da oportunidade (pode ser vazio → "Oportunidade — {conta}").</summary>
    public const string TituloOportunidade = "Título da Oportunidade";

    /// <summary>Etapa do funil de vendas.</summary>
    public const string Etapa = "Etapa";

    /// <summary>Número AZ-NNNN da oportunidade (preservado ou gerado).</summary>
    public const string Numero = "Nº";

    /// <summary>Valor de setup em R$ (convertido para centavos).</summary>
    public const string ValorSetup = "Valor Setup (R$)";

    /// <summary>Valor mensal recorrente em R$ (convertido para centavos).</summary>
    public const string ValorMensal = "Valor Mensal (R$)";

    /// <summary>Número de meses do contrato.</summary>
    public const string Meses = "Meses";

    /// <summary>Probabilidade de fechamento em % (inteiro 0-100).</summary>
    public const string Probabilidade = "Probabilidade (%)";

    /// <summary>Forecast ponderado da planilha em R$ (para verificação de divergência).</summary>
    public const string ForecastPonderado = "Forecast (R$)";

    /// <summary>Nome do parceiro (pode ser "-" para sem parceiro).</summary>
    public const string Parceiro = "Parceiro";

    /// <summary>Data prevista de fechamento (serial Excel).</summary>
    public const string DataFechamento = "Data de Fechamento";

    // =========================================================================
    // Aba Ações Comerciais (required)
    // =========================================================================

    /// <summary>Número AZ-NNNN da oportunidade relacionada.</summary>
    public const string NumeroOportunidade = "Nº Oportunidade";

    /// <summary>Descrição da atividade comercial.</summary>
    public const string Descricao = "Descrição";

    /// <summary>Data da atividade (serial Excel).</summary>
    public const string DataAtividade = "Data";

    /// <summary>Tipo de atividade (reunião, proposta, etc.).</summary>
    public const string TipoAtividade = "Tipo";

    // =========================================================================
    // Conjunto completo de colunas obrigatórias (validação de estrutura)
    // =========================================================================

    /// <summary>
    /// Todas as colunas obrigatórias da aba Pipeline.
    /// Usado na validação de estrutura da planilha enviada (Req 1.2, MIG-ERR-002).
    /// </summary>
    public static readonly IReadOnlyList<string> PipelineRequired =
    [
        Bu, Conta, Responsavel, TituloOportunidade, Etapa, Numero,
        ValorSetup, ValorMensal, Meses, Probabilidade, ForecastPonderado,
        Parceiro, DataFechamento,
    ];

    /// <summary>
    /// Todas as colunas obrigatórias da aba Ações Comerciais.
    /// </summary>
    public static readonly IReadOnlyList<string> AcoesRequeridas =
    [
        NumeroOportunidade, Descricao, DataAtividade, TipoAtividade,
    ];

    /// <summary>
    /// Todas as colunas obrigatórias do módulo (Pipeline + Ações Comerciais).
    /// </summary>
    public static IReadOnlyList<string> All =>
        PipelineRequired.Concat(AcoesRequeridas).ToList().AsReadOnly();
}

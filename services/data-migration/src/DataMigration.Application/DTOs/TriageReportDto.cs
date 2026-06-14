using DataMigration.Domain.ValueObjects;

namespace DataMigration.Application.DTOs;

/// <summary>
/// Divergência de forecast detectada durante o dry-run.
/// Sem PII — referência por índice de linha.
/// </summary>
/// <param name="SourceRowIndex">Índice base-0 da linha na aba.</param>
/// <param name="ForecastPlanilhaCents">Forecast informado na planilha em centavos.</param>
/// <param name="ForecastCalculadoCents">Forecast recalculado em centavos (round_half_even).</param>
/// <param name="DeltaCents">Diferença calculado − planilha em centavos.</param>
public sealed record ForecastDivergenceEntry(
    int SourceRowIndex,
    long ForecastPlanilhaCents,
    long ForecastCalculadoCents,
    long DeltaCents);

/// <summary>
/// Par de contas candidatos a dedupe.
/// Sem PII — referência por índice de linha (RNF 3).
/// </summary>
/// <param name="RowIndexA">Índice da primeira linha.</param>
/// <param name="RowIndexB">Índice da segunda linha.</param>
/// <param name="NormalizedName">Nome normalizado compartilhado.</param>
public sealed record DedupeCandidatePair(
    int RowIndexA,
    int RowIndexB,
    string NormalizedName);

/// <summary>
/// Distribuição de oportunidades por Business Unit.
/// </summary>
/// <param name="BuName">Nome da BU.</param>
/// <param name="Count">Total de oportunidades na BU.</param>
public sealed record BuDistributionEntry(string BuName, int Count);

/// <summary>
/// Relatório gerado após o dry-run com contagens, flags e divergências.
///
/// Sem PII (design §4.3, RNF 3, DD-007). Persistido como snapshot JSONB
/// no <c>MigrationJob.TriageReportJson</c>.
///
/// Rastreia: design §5.1, §5.3, Req 2, PBT-04, TASK-09.
/// </summary>
public sealed class TriageReportDto
{
    /// <summary>Total de oportunidades detectadas na aba Pipeline.</summary>
    public int TotalOpportunities { get; init; }

    /// <summary>Total de contas distintas pós-dedupe.</summary>
    public int TotalAccounts { get; init; }

    /// <summary>Distribuição de oportunidades por BU.</summary>
    public IReadOnlyList<BuDistributionEntry> ByBu { get; init; } = [];

    /// <summary>Número de oportunidades sem owner atribuído (bloqueante).</summary>
    public int OwnerMissingCount { get; init; }

    /// <summary>Número de oportunidades sem data de fechamento.</summary>
    public int CloseDateMissingCount { get; init; }

    /// <summary>Número de oportunidades com parceiro sem percentual.</summary>
    public int PartnerPctMissingCount { get; init; }

    /// <summary>Número de pares candidatos a dedupe.</summary>
    public int DedupeCandidateCount { get; init; }

    /// <summary>Número de possíveis typos de owner detectados.</summary>
    public int TypoCount { get; init; }

    /// <summary>Número de oportunidades sem estágio (fallback "Lead" aplicado).</summary>
    public int StageMissingCount { get; init; }

    /// <summary>Todos os flags de triagem por linha.</summary>
    public IReadOnlyList<TriageFlag> Flags { get; init; } = [];

    /// <summary>Pares candidatos a dedupe.</summary>
    public IReadOnlyList<DedupeCandidatePair> DedupeCandidates { get; init; } = [];

    /// <summary>Divergências de forecast (|Δ| > 1 centavo).</summary>
    public IReadOnlyList<ForecastDivergenceEntry> ForecastDivergences { get; init; } = [];

    /// <summary>Total de atividades detectadas na aba Ações Comerciais.</summary>
    public int TotalActivities { get; init; }
}

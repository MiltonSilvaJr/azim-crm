namespace PartnerManagement.Contracts.Commissions;

/// <summary>
/// DTO de response para a visão de comissão de um parceiro (projetada × consolidada).
/// Valores monetários em centavos inteiros (money-as-cents, RNF 6).
/// Este módulo não calcula comissão: os valores vêm do read model do pipeline (DD-003).
/// Mapeia: design §8, design §9, Req 9, PBT-01, PBT-05, TASK-22.
/// </summary>
public sealed class CommissionViewResponse
{
    /// <summary>Identificador do parceiro.</summary>
    public Guid PartnerId { get; init; }

    /// <summary>Início do período consultado (UTC).</summary>
    public DateTimeOffset PeriodFrom { get; init; }

    /// <summary>Fim do período consultado (UTC).</summary>
    public DateTimeOffset PeriodTo { get; init; }

    /// <summary>
    /// Soma das comissões projetadas (oportunidades abertas) em centavos inteiros.
    /// Zero quando não há oportunidades abertas no período.
    /// </summary>
    public long ProjectedCommissionCents { get; init; }

    /// <summary>
    /// Soma das comissões consolidadas (snapshots de oportunidades ganhas) em centavos inteiros.
    /// Imutável após consolidação (PBT-05, DD-003).
    /// </summary>
    public long ConsolidatedCommissionCents { get; init; }

    /// <summary>
    /// Verdadeiro quando o read model do pipeline está indisponível e os valores de comissão
    /// não puderam ser obtidos. Os dados cadastrais do parceiro ainda são retornados (design §5.3).
    /// </summary>
    public bool CommissionUnavailable { get; init; }
}

/// <summary>
/// DTO de response para o relatório de comissões por oportunidade.
/// Mapeia: design §8, Req 10, TASK-22.
/// </summary>
public sealed class CommissionReportResponse
{
    /// <summary>Identificador do parceiro.</summary>
    public Guid PartnerId { get; init; }

    /// <summary>Início do período consultado (UTC).</summary>
    public DateTimeOffset PeriodFrom { get; init; }

    /// <summary>Fim do período consultado (UTC).</summary>
    public DateTimeOffset PeriodTo { get; init; }

    /// <summary>Linhas do relatório (uma por oportunidade).</summary>
    public IReadOnlyList<CommissionReportLine> Lines { get; init; } = [];

    /// <summary>
    /// Verdadeiro quando o read model do pipeline está indisponível.
    /// </summary>
    public bool CommissionUnavailable { get; init; }
}

/// <summary>
/// Linha do relatório de comissão correspondendo a uma oportunidade.
/// </summary>
public sealed class CommissionReportLine
{
    /// <summary>Identificador da oportunidade.</summary>
    public Guid OpportunityId { get; init; }

    /// <summary>Valor da comissão em centavos inteiros (money-as-cents).</summary>
    public long CommissionCents { get; init; }

    /// <summary>
    /// <c>true</c> = comissão consolidada (snapshot de oportunidade ganha);
    /// <c>false</c> = comissão projetada (oportunidade aberta).
    /// </summary>
    public bool IsSnapshot { get; init; }

    /// <summary>Data de registro da comissão no pipeline (UTC).</summary>
    public DateTimeOffset OccurredAt { get; init; }
}

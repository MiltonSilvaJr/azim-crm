namespace Reporting.Contracts.Responses;

/// <summary>
/// Linha agregada do relatório de comissões por parceiro e moeda (ADR-0008).
/// Relatórios NUNCA somam moedas distintas — cada linha representa um par (parceiro, moeda).
/// Mapeia: TASK-10, design §5.2.
/// </summary>
public sealed record CommissionPartnerRow(
    Guid PartnerId,
    string PartnerName,
    long ProjectedCents,
    long ConsolidatedCents,
    int OpportunityCount,
    string Currency = "BRL");

/// <summary>
/// Resposta do relatório de comissões por parceiro (Req 4).
/// <see cref="CommissionPartnerRow.ConsolidatedCents"/> derivado exclusivamente de snapshots imutáveis (PBT-01, RN-007).
/// Linhas agrupadas por (PartnerId, Currency) — nunca somadas entre moedas diferentes (ADR-0008).
/// Mapeia: TASK-10, design §5.2.
/// </summary>
public sealed record CommissionReportResponse(IReadOnlyList<CommissionPartnerRow> Rows);

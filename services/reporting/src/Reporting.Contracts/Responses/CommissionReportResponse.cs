namespace Reporting.Contracts.Responses;

/// <summary>
/// Linha agregada do relatório de comissões por parceiro.
/// Mapeia: TASK-10, design §5.2.
/// </summary>
public sealed record CommissionPartnerRow(
    Guid PartnerId,
    string PartnerName,
    long ProjectedCents,
    long ConsolidatedCents,
    int OpportunityCount);

/// <summary>
/// Resposta do relatório de comissões por parceiro (Req 4).
/// <see cref="CommissionPartnerRow.ConsolidatedCents"/> derivado exclusivamente de snapshots imutáveis (PBT-01, RN-007).
/// Mapeia: TASK-10, design §5.2.
/// </summary>
public sealed record CommissionReportResponse(IReadOnlyList<CommissionPartnerRow> Rows);

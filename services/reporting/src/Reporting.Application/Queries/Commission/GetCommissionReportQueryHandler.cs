using MediatR;
using Reporting.Application.Ports;
using Reporting.Contracts.ReadModels;
using Reporting.Contracts.Responses;

namespace Reporting.Application.Queries.Commission;

/// <summary>
/// Handler do relatório de comissões por parceiro (Req 4).
///
/// Regras de agregação (PBT-01, PBT-02, RN-007):
/// <list type="bullet">
///   <item><description><c>ConsolidatedCents</c> = SUM(<c>CommissionCents</c>) WHERE <c>IsSnapshot = true</c>.</description></item>
///   <item><description><c>ProjectedCents</c> = SUM(<c>CommissionCents</c>) WHERE <c>IsSnapshot = false</c> AND <c>StageCategory = "open"</c>.</description></item>
///   <item><description>O handler NÃO reimplementa a fórmula de comissão — apenas soma os valores autoritativos do repositório.</description></item>
///   <item><description>Somas em aritmética inteira (<c>long</c>) — sem float/double (DD-007, PBT-02).</description></item>
/// </list>
///
/// Mapeia: TASK-10, design §5.3, Req 4, Req 4.2, Req 4.3, DD-007, PBT-01, PBT-02, RN-007.
/// </summary>
public sealed class GetCommissionReportQueryHandler : IRequestHandler<GetCommissionReportQuery, CommissionReportResponse>
{
    private readonly IReportingReadRepository _repository;

    /// <summary>Inicializa o handler com o repositório de leitura.</summary>
    public GetCommissionReportQueryHandler(IReportingReadRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <inheritdoc/>
    public async Task<CommissionReportResponse> Handle(
        GetCommissionReportQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rawRows = await _repository.GetCommissionsAsync(
            request.Scope,
            request.Period,
            request.BuIds,
            cancellationToken);

        var aggregated = AggregateByPartner(rawRows);
        return new CommissionReportResponse(aggregated);
    }

    /// <summary>
    /// Agrega as linhas brutas por parceiro, separando snapshot de projetado.
    /// Lógica pura testável — sem IO.
    /// </summary>
    public static IReadOnlyList<CommissionPartnerRow> AggregateByPartner(IReadOnlyList<CommissionRow> rawRows)
    {
        // Agrupa por partnerId
        var grouped = rawRows.GroupBy(r => r.PartnerId);

        var result = new List<CommissionPartnerRow>();
        foreach (var group in grouped)
        {
            var rows       = group.ToList();
            var partnerName = rows[0].PartnerName;

            // ConsolidatedCents: apenas snapshots (RN-007, Req 4.2, PBT-01)
            // Soma em long — sem float/double (DD-007, PBT-02)
            var consolidatedCents = rows
                .Where(r => r.IsSnapshot)
                .Aggregate(0L, (acc, r) => acc + r.CommissionCents);

            // ProjectedCents: não-snapshot com stageCategory = "open" (Req 4.3)
            var projectedCents = rows
                .Where(r => !r.IsSnapshot && r.StageCategory == "open")
                .Aggregate(0L, (acc, r) => acc + r.CommissionCents);

            var opportunityCount = rows.Select(r => r.PartnerId).Distinct().Count(); // por parceiro
            opportunityCount = rows.Count; // total de linhas por parceiro

            result.Add(new CommissionPartnerRow(
                group.Key,
                partnerName,
                projectedCents,
                consolidatedCents,
                opportunityCount));
        }

        return result;
    }
}

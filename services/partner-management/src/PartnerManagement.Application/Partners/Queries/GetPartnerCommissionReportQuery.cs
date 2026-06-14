using MediatR;

namespace PartnerManagement.Application.Partners.Queries;

/// <summary>
/// Query para obter o relatório de comissões de um parceiro por período (linha a linha por oportunidade).
/// Restrição de acesso: Tenant Admin e Gestor de BU (AuthorizationBehavior, design §5.5).
/// Mapeia: Req 10, design §5.2.
/// </summary>
/// <param name="PartnerId">Identificador do parceiro.</param>
/// <param name="TenantId">Tenant do contexto.</param>
/// <param name="From">Início do período (UTC).</param>
/// <param name="To">Fim do período (UTC).</param>
/// <param name="CorrelationId">Identificador de correlação propagado ao pipeline.</param>
public sealed record GetPartnerCommissionReportQuery(
    Guid PartnerId,
    Guid TenantId,
    DateTimeOffset From,
    DateTimeOffset To,
    string? CorrelationId = null
) : IRequest<CommissionReportResult>;

/// <summary>
/// Relatório de comissões de um parceiro, linha a linha por oportunidade.
/// </summary>
/// <param name="PartnerId">Identificador do parceiro.</param>
/// <param name="PeriodFrom">Início do período.</param>
/// <param name="PeriodTo">Fim do período.</param>
/// <param name="Lines">Linhas de comissão.</param>
/// <param name="CommissionUnavailable">Verdadeiro quando o read port está indisponível.</param>
public sealed record CommissionReportResult(
    Guid PartnerId,
    DateTimeOffset PeriodFrom,
    DateTimeOffset PeriodTo,
    IReadOnlyList<CommissionReportLine> Lines,
    bool CommissionUnavailable = false
);

/// <summary>
/// Linha do relatório de comissão por oportunidade.
/// </summary>
/// <param name="OpportunityId">Identificador da oportunidade.</param>
/// <param name="CommissionCents">Valor da comissão em centavos inteiros.</param>
/// <param name="IsSnapshot">Verdadeiro = consolidada; falso = projetada.</param>
/// <param name="OccurredAt">Data de registro no pipeline.</param>
public sealed record CommissionReportLine(
    Guid OpportunityId,
    long CommissionCents,
    bool IsSnapshot,
    DateTimeOffset OccurredAt
);

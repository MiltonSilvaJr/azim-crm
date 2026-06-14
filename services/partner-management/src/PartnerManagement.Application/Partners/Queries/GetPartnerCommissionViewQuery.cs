using MediatR;

namespace PartnerManagement.Application.Partners.Queries;

/// <summary>
/// Query para obter a visão de comissão de um parceiro (projetada × consolidada).
/// Compõe dados do cadastro <c>partners</c> com o read model <c>opportunity_partner_commissions</c>
/// via <see cref="PartnerManagement.Application.Ports.IPartnerCommissionReadPort"/>.
/// Em indisponibilidade do read port, retorna dados de cadastro com seção de comissão marcada
/// como indisponível (design §5.3, design §15).
/// Mapeia: Req 9, PBT-01, PBT-05, DD-003, design §5.2.
/// </summary>
/// <param name="PartnerId">Identificador do parceiro.</param>
/// <param name="TenantId">Tenant do contexto.</param>
/// <param name="From">Início do período (UTC).</param>
/// <param name="To">Fim do período (UTC).</param>
/// <param name="CorrelationId">Identificador de correlação propagado ao pipeline.</param>
public sealed record GetPartnerCommissionViewQuery(
    Guid PartnerId,
    Guid TenantId,
    DateTimeOffset From,
    DateTimeOffset To,
    string? CorrelationId = null
) : IRequest<CommissionViewResult>;

/// <summary>
/// Resultado da visão de comissão de um parceiro.
/// </summary>
/// <param name="PartnerId">Identificador do parceiro.</param>
/// <param name="PeriodFrom">Início do período.</param>
/// <param name="PeriodTo">Fim do período.</param>
/// <param name="ProjectedCommissionCents">
/// Soma das comissões das oportunidades abertas (centavos inteiros, Req 9.2).
/// </param>
/// <param name="ConsolidatedCommissionCents">
/// Soma das comissões consolidadas (snapshots de oportunidades ganhas, Req 9.3).
/// </param>
/// <param name="CommissionUnavailable">
/// Verdadeiro quando o read port está indisponível e a seção de comissão não pôde ser calculada.
/// </param>
public sealed record CommissionViewResult(
    Guid PartnerId,
    DateTimeOffset PeriodFrom,
    DateTimeOffset PeriodTo,
    long ProjectedCommissionCents,
    long ConsolidatedCommissionCents,
    bool CommissionUnavailable = false
);

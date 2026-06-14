using MediatR;

namespace PartnerManagement.Application.Partners.Queries;

/// <summary>
/// Query para obter o status de elegibilidade de um parceiro para vinculação a oportunidades.
/// Consumida internamente pelo opportunity-pipeline via mTLS (Req 8.1, DD-007).
/// Retorna PM-ERR-007 quando o parceiro não existe ou está fora do tenant.
/// Mapeia: Req 8, design §5.2.
/// </summary>
/// <param name="PartnerId">Identificador do parceiro.</param>
/// <param name="TenantId">Tenant do contexto.</param>
public sealed record GetPartnerEligibilityQuery(
    Guid PartnerId,
    Guid TenantId
) : IRequest<PartnerEligibilityResult>;

/// <summary>
/// Status de elegibilidade do parceiro.
/// </summary>
/// <param name="PartnerId">Identificador do parceiro.</param>
/// <param name="Active">Verdadeiro se o parceiro está ativo e elegível à vinculação.</param>
public sealed record PartnerEligibilityResult(
    Guid PartnerId,
    bool Active
);

using MediatR;

namespace PartnerManagement.Application.Partners.Queries;

/// <summary>
/// Query para obter o detalhe de um parceiro pelo identificador.
/// Retorna PM-ERR-007 via exceção quando o parceiro não existe ou está fora do tenant.
/// Mapeia: Req 2, Req 4, design §5.2.
/// </summary>
/// <param name="PartnerId">Identificador do parceiro.</param>
/// <param name="TenantId">Tenant do contexto (validação anti-enumeração).</param>
public sealed record GetPartnerByIdQuery(
    Guid PartnerId,
    Guid TenantId
) : IRequest<PartnerDetail>;

/// <summary>Detalhe completo de um parceiro.</summary>
/// <param name="PartnerId">Identificador do parceiro.</param>
/// <param name="Name">Nome.</param>
/// <param name="Role">Papel tipado.</param>
/// <param name="PctSetup">Percentual de setup.</param>
/// <param name="PctRecorrente">Percentual de recorrência.</param>
/// <param name="ContactEmail">E-mail de contato (mascarado em logs; exposto no response autenticado).</param>
/// <param name="ContactPhone">Telefone de contato.</param>
/// <param name="Notes">Observações.</param>
/// <param name="Active">Status do parceiro.</param>
/// <param name="IsTriagePending">Derivado: ambos percentuais em 0,00.</param>
/// <param name="CreatedAt">Momento de criação (UTC).</param>
/// <param name="UpdatedAt">Momento da última atualização (UTC).</param>
public sealed record PartnerDetail(
    Guid PartnerId,
    string Name,
    string Role,
    decimal PctSetup,
    decimal PctRecorrente,
    string? ContactEmail,
    string? ContactPhone,
    string? Notes,
    bool Active,
    bool IsTriagePending,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);

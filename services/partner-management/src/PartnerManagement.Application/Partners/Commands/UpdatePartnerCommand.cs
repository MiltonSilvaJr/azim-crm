using MediatR;
using PartnerManagement.Domain.Partners.ValueObjects;

namespace PartnerManagement.Application.Partners.Commands;

/// <summary>
/// Comando para atualizar o perfil de um parceiro existente.
/// Invariantes delegadas ao domínio via <c>Partner.UpdateProfile</c> e <c>Partner.UpdateContact</c>.
/// Mapeia: Req 2, Req 5, Req 6, Req 7, design §5.1.
/// </summary>
/// <param name="PartnerId">Identificador do parceiro a ser atualizado.</param>
/// <param name="TenantId">Tenant do contexto (validação anti-enumeração).</param>
/// <param name="Name">Novo nome do parceiro.</param>
/// <param name="Role">Novo papel tipado canônico.</param>
/// <param name="CommissionDefaults">Novos percentuais padrão.</param>
/// <param name="ContactEmail">Novo e-mail de contato (null = sem alteração de contato).</param>
/// <param name="ContactPhone">Novo telefone de contato.</param>
/// <param name="Notes">Novas observações.</param>
/// <param name="UpdatedBy">Identificador do usuário autor da alteração.</param>
public sealed record UpdatePartnerCommand(
    Guid PartnerId,
    Guid TenantId,
    string Name,
    string Role,
    CommissionDefaults CommissionDefaults,
    string? ContactEmail,
    string? ContactPhone,
    string? Notes,
    Guid UpdatedBy
) : IRequest<UpdatePartnerResult>;

/// <summary>
/// Resultado da atualização de um parceiro.
/// </summary>
public sealed record UpdatePartnerResult();

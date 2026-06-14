using MediatR;
using PartnerManagement.Domain.Partners.ValueObjects;

namespace PartnerManagement.Application.Partners.Commands;

/// <summary>
/// Comando para criar um novo parceiro.
/// Invariantes delegadas ao domínio via <c>Partner.Create</c>.
/// Nome duplicado gera alerta (MSG-021) sem bloquear a criação (<c>ConfirmCreateDespiteDuplicate</c>).
/// Aceita percentuais 0,00/0,00 quando originado pelo data-migration (Req 11.1).
/// Mapeia: Req 1, Req 5, Req 6, Req 7, Req 11, design §5.1.
/// </summary>
/// <param name="TenantId">Tenant ao qual o parceiro será associado.</param>
/// <param name="Name">Nome do parceiro.</param>
/// <param name="Role">Papel tipado canônico do parceiro.</param>
/// <param name="CommissionDefaults">Percentuais padrão (pode ser Default 0,00/0,00).</param>
/// <param name="ContactEmail">E-mail de contato opcional.</param>
/// <param name="ContactPhone">Telefone de contato opcional.</param>
/// <param name="Notes">Observações opcionais.</param>
/// <param name="CreatedBy">Identificador do usuário autor da criação.</param>
/// <param name="ConfirmCreateDespiteDuplicate">
/// Confirma criação mesmo que nome duplicado tenha sido alertado (MSG-021).
/// </param>
public sealed record CreatePartnerCommand(
    Guid TenantId,
    string Name,
    string Role,
    CommissionDefaults CommissionDefaults,
    string? ContactEmail,
    string? ContactPhone,
    string? Notes,
    Guid CreatedBy,
    bool ConfirmCreateDespiteDuplicate = false
) : IRequest<CreatePartnerResult>;

/// <summary>
/// Resultado da criação de um parceiro.
/// </summary>
/// <param name="PartnerId">Identificador do parceiro criado.</param>
/// <param name="DuplicateNameAlert">
/// Verdadeiro quando existe parceiro com nome igual no tenant (MSG-021).
/// </param>
public sealed record CreatePartnerResult(
    Guid PartnerId,
    bool DuplicateNameAlert
);

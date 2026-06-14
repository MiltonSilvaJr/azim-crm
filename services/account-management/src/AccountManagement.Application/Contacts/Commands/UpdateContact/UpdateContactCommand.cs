using AccountManagement.Application.Behaviors;
using MediatR;

namespace AccountManagement.Application.Contacts.Commands.UpdateContact;

/// <summary>
/// Command para atualizar os dados de PII de um contato existente.
///
/// Marcado como <see cref="IPiiSensitiveRequest"/> — exige papel mínimo Vendedor (Req 9.1, RNF 6).
/// Gera <c>ContactLinked</c> com <c>maskedDelta</c> sem PII (DD-003).
///
/// Mapeia: design §5.1, Req 5, ACC-ERR-004, ACC-ERR-005, ACC-ERR-006, ACC-ERR-008.
/// </summary>
/// <param name="AccountId">Conta à qual o contato pertence.</param>
/// <param name="ContactId">Identificador do contato a atualizar.</param>
/// <param name="Name">Novo nome do contato (obrigatório).</param>
/// <param name="Email">Novo e-mail (opcional).</param>
/// <param name="Phone">Novo telefone (opcional).</param>
/// <param name="Role">Novo cargo (opcional).</param>
public sealed record UpdateContactCommand(
    Guid AccountId,
    Guid ContactId,
    string Name,
    string? Email,
    string? Phone,
    string? Role) : IRequest, IPiiSensitiveRequest, ICommand;

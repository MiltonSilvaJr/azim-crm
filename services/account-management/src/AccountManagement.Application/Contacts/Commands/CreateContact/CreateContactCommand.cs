using AccountManagement.Application.Behaviors;
using MediatR;

namespace AccountManagement.Application.Contacts.Commands.CreateContact;

/// <summary>
/// Command para criar um contato vinculado a uma conta.
///
/// Marcado como <see cref="IPiiSensitiveRequest"/> — o <see cref="PiiAccessBehavior{TRequest,TResponse}"/>
/// exige papel mínimo Vendedor (Req 9.1, RNF 6).
///
/// Mapeia: design §5.1, Req 5, ACC-ERR-004, ACC-ERR-005, ACC-ERR-008, DD-003.
/// </summary>
/// <param name="AccountId">Conta à qual o contato será vinculado.</param>
/// <param name="Name">Nome do contato (obrigatório — ACC-ERR-005).</param>
/// <param name="Email">E-mail do contato (opcional — ACC-ERR-004 se inválido).</param>
/// <param name="Phone">Telefone do contato (opcional).</param>
/// <param name="Role">Cargo/papel do contato (opcional).</param>
public sealed record CreateContactCommand(
    Guid AccountId,
    string Name,
    string? Email,
    string? Phone,
    string? Role) : IRequest, IPiiSensitiveRequest, ICommand;

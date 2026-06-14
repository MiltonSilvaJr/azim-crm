using AccountManagement.Application.Behaviors;
using MediatR;

namespace AccountManagement.Application.Contacts.Commands.ForgetContact;

/// <summary>
/// Command para anonimizar um contato (direito ao esquecimento LGPD).
///
/// Exige papel Tenant Admin (<see cref="IForgetContactRequest"/> — PiiAccessBehavior).
/// A operação é irreversível: PII substituída por marcador, <c>contact_id</c> preservado (DD-001).
///
/// Mapeia: design §5.1, Req 7, ACC-ERR-006, ACC-ERR-007, ACC-ERR-008, DD-001.
/// </summary>
/// <param name="AccountId">Conta à qual o contato pertence.</param>
/// <param name="ContactId">Identificador do contato a anonimizar.</param>
public sealed record ForgetContactCommand(
    Guid AccountId,
    Guid ContactId) : IRequest, IForgetContactRequest, ICommand;

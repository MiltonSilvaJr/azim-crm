using AccountManagement.Application.Behaviors;
using AccountManagement.Domain.Accounts;
using MediatR;

namespace AccountManagement.Application.Contacts.Queries.ListContacts;

/// <summary>
/// Query para listar os contatos de uma conta.
///
/// Acesso controlado por <see cref="IPiiSensitiveRequest"/>: exige papel mínimo
/// Vendedor (na BU) para retornar PII dos contatos (Req 9, RNF 6).
///
/// Mapeia: design §5.2, Req 5, Req 9, RNF 6, ACC-ERR-003, ACC-ERR-008.
/// </summary>
/// <param name="AccountId">Identificador da conta cujos contatos serão listados.</param>
public sealed record ListContactsQuery(Guid AccountId)
    : IRequest<IReadOnlyList<Contact>>, IPiiSensitiveRequest;

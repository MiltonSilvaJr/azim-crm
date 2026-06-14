using AccountManagement.Application.Exceptions;
using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.Repositories;
using MediatR;

namespace AccountManagement.Application.Contacts.Queries.ListContacts;

/// <summary>
/// Handler para <see cref="ListContactsQuery"/>.
///
/// Carrega o agregado <see cref="Account"/> e retorna os contatos via
/// <see cref="Account.Contacts"/>. O acesso à PII dos contatos é protegido
/// pelo <c>PiiAccessBehavior</c> (exige papel Vendedor — Req 9, RNF 6).
///
/// Lança <see cref="AccountNotFoundException"/> quando a conta não é encontrada
/// (anti-enumeração — ACC-ERR-003).
///
/// Mapeia: design §5.2, §5.3, Req 5, Req 9, RNF 6, ACC-ERR-003.
/// </summary>
internal sealed class ListContactsHandler : IRequestHandler<ListContactsQuery, IReadOnlyList<Contact>>
{
    private readonly IAccountRepository _repository;

    /// <summary>Inicializa o handler com suas dependências.</summary>
    public ListContactsHandler(IAccountRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Contact>> Handle(
        ListContactsQuery request,
        CancellationToken cancellationToken)
    {
        var account = await _repository.GetByIdAsync(request.AccountId, cancellationToken)
            ?? throw new AccountNotFoundException();

        return account.Contacts;
    }
}

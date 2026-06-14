using AccountManagement.Application.Exceptions;
using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.Repositories;
using MediatR;

namespace AccountManagement.Application.Accounts.Queries.GetAccountById;

/// <summary>
/// Handler para <see cref="GetAccountByIdQuery"/>.
///
/// Carrega o agregado via repositório (com filtro global de tenant ativo).
/// Lança <see cref="AccountNotFoundException"/> quando a conta não existe ou está fora do tenant
/// (sem distinguir os dois casos — anti-enumeração — Req 9, PBT-04).
///
/// Mapeia: design §5.2, §5.3, Req 3, ACC-ERR-003.
/// </summary>
internal sealed class GetAccountByIdHandler : IRequestHandler<GetAccountByIdQuery, Account>
{
    private readonly IAccountRepository _repository;

    /// <summary>Inicializa o handler com o repositório.</summary>
    public GetAccountByIdHandler(IAccountRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<Account> Handle(GetAccountByIdQuery request, CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(request.AccountId, cancellationToken)
            ?? throw new AccountNotFoundException();
    }
}

using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.Repositories;
using MediatR;

namespace AccountManagement.Application.Accounts.Queries.SearchAccounts;

/// <summary>
/// Handler para <see cref="SearchAccountsQuery"/>.
///
/// Delega a busca paginada ao repositório. O isolamento de tenant é garantido pelo
/// filtro global do DbContext resolvido via <c>TenantScopeBehavior</c> (design §5.4).
///
/// Mapeia: design §5.2, §5.3, Req 3, RNF 7.
/// </summary>
internal sealed class SearchAccountsHandler : IRequestHandler<SearchAccountsQuery, IReadOnlyList<Account>>
{
    private readonly IAccountRepository _repository;

    /// <summary>Inicializa o handler com o repositório.</summary>
    public SearchAccountsHandler(IAccountRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Account>> Handle(
        SearchAccountsQuery request,
        CancellationToken cancellationToken) =>
        _repository.SearchAsync(request.Search, request.Page, request.PageSize, cancellationToken);
}

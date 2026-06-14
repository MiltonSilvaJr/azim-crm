using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.Repositories;
using AccountManagement.Domain.Accounts.Services;
using AccountManagement.Domain.Accounts.ValueObjects;
using MediatR;

namespace AccountManagement.Application.Accounts.Queries.SearchAccounts;

/// <summary>
/// Handler para <see cref="SearchSimilarAccountsQuery"/>.
///
/// Normaliza o nome informado via <see cref="NameNormalizer"/> e busca contas do tenant
/// com a mesma forma normalizada. O isolamento de tenant é garantido pelo filtro global (DD-002).
///
/// Mapeia: design §5.2, §5.3, Req 1.2, DD-005, DD-006.
/// </summary>
internal sealed class SearchSimilarAccountsHandler
    : IRequestHandler<SearchSimilarAccountsQuery, IReadOnlyList<Account>>
{
    private readonly IAccountRepository _repository;
    private readonly NameNormalizer _normalizer;

    /// <summary>Inicializa o handler.</summary>
    public SearchSimilarAccountsHandler(IAccountRepository repository)
    {
        _repository = repository;
        _normalizer = new NameNormalizer();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Account>> Handle(
        SearchSimilarAccountsQuery request,
        CancellationToken cancellationToken)
    {
        var normalizedName = _normalizer.NormalizeName(request.Name);
        return _repository.SearchSimilarAsync(normalizedName, cancellationToken);
    }
}

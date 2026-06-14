using AccountManagement.Domain.Accounts;
using MediatR;

namespace AccountManagement.Application.Accounts.Queries.SearchAccounts;

/// <summary>
/// Query para buscar contas similares ao nome fornecido dentro do tenant.
///
/// Usada pela UI antes do <c>POST /accounts</c> para alertar o usuário sobre possíveis duplicatas
/// (dedupe não-bloqueante — DD-006). O nome é normalizado antes da busca (DD-005).
///
/// Mapeia: design §5.2, Req 1.2, DD-005, DD-006, PBT-02.
/// </summary>
/// <param name="Name">Nome a buscar (normalizado internamente pelo handler).</param>
public sealed record SearchSimilarAccountsQuery(string Name) : IRequest<IReadOnlyList<Account>>;

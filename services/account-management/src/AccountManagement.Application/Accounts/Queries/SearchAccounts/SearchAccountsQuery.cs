using AccountManagement.Domain.Accounts;
using MediatR;

namespace AccountManagement.Application.Accounts.Queries.SearchAccounts;

/// <summary>
/// Query para buscar/listar contas do tenant por texto no nome.
///
/// A busca é paginada e restrita ao tenant do contexto (filtro global via TenantScopeBehavior).
///
/// Mapeia: design §5.2, Req 3, RNF 7, ACC-ERR-002.
/// </summary>
/// <param name="Search">Texto de busca (opcional; null retorna todas as contas).</param>
/// <param name="Page">Número da página (base 1 — ACC-ERR-002 se menor que 1).</param>
/// <param name="PageSize">Tamanho da página (1–100 — ACC-ERR-002 se fora do intervalo).</param>
public sealed record SearchAccountsQuery(
    string? Search,
    int Page,
    int PageSize) : IRequest<IReadOnlyList<Account>>;

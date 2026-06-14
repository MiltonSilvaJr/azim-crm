using AccountManagement.Domain.Accounts.ValueObjects;

namespace AccountManagement.Domain.Accounts.Repositories;

/// <summary>
/// Porta de saída para persistência do agregado <see cref="Account"/>.
///
/// Definida no Domain; implementada na Infrastructure (regra de dependência — design §3).
/// O repositório não vaza <c>IQueryable</c> nem <c>DbSet</c> para fora da Infrastructure.
///
/// Isolamento multi-tenant: o <c>tenant_id</c> é implícito no contexto do repositório
/// (resolvido via <c>TenantContext</c> e filtro global EF Core + RLS — DD-002, ADR-0001).
/// Nunca é parâmetro explícito nas assinaturas públicas.
///
/// Mapeia: design §4.1, design §6.1, Req 10, RNF 5, DD-002.
/// </summary>
public interface IAccountRepository
{
    /// <summary>
    /// Retorna a conta com o identificador fornecido incluindo todos os contatos.
    /// Retorna <c>null</c> quando a conta não existe ou não pertence ao tenant do contexto.
    /// </summary>
    /// <param name="id">Identificador da conta.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca contas por texto no nome normalizado (ou trecho de nome).
    /// Restrita ao tenant do contexto; paginada.
    /// </summary>
    /// <param name="search">Texto de busca (normalizado internamente).</param>
    /// <param name="page">Número da página (base 1).</param>
    /// <param name="pageSize">Tamanho da página.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<IReadOnlyList<Account>> SearchAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna candidatos a duplicata: contas do mesmo tenant com a forma normalizada
    /// exatamente igual à fornecida (dedupe não-bloqueante — DD-006).
    /// </summary>
    /// <param name="normalizedName">Forma normalizada do nome a buscar.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<IReadOnlyList<Account>> SearchSimilarAsync(
        NormalizedName normalizedName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste (insere ou atualiza) o agregado <see cref="Account"/> incluindo seus contatos.
    /// O despacho de domain events é responsabilidade do <c>TransactionBehavior</c> (DD-007).
    /// </summary>
    /// <param name="account">Agregado a persistir.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task SaveAsync(Account account, CancellationToken cancellationToken = default);
}

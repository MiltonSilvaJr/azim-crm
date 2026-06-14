using GoalForecast.Domain.Aggregates;

namespace GoalForecast.Application.Ports;

/// <summary>
/// Porta de saída para persistência do aggregate <see cref="Goal"/>.
/// Implementada na camada Infrastructure (GoalRepository). Handlers dependem
/// apenas desta interface — sem vazamento de IQueryable ou DbContext.
///
/// Intenções de domínio expostas:
/// <list type="bullet">
///   <item><term>FindByKey</term><description>Upsert por chave natural (DD-002).</description></item>
///   <item><term>FindById</term><description>Leitura por identidade UUID.</description></item>
///   <item><term>Add</term><description>Insere novo aggregate e despacha domain events.</description></item>
///   <item><term>Update</term><description>Persiste alterações e despacha domain events.</description></item>
///   <item><term>Query</term><description>Listagem filtrada com paginação.</description></item>
/// </list>
///
/// Mapeia: Req 1, Req 2, Req 3, DD-002, design §5.3, §6.1, TASK-08.
/// </summary>
public interface IGoalRepository
{
    /// <summary>
    /// Busca meta pela chave natural de upsert.
    /// Retorna <c>null</c> quando não existe meta para a combinação.
    /// </summary>
    /// <param name="tenantId">Tenant do principal autenticado (INV-4).</param>
    /// <param name="buId">Identificador da BU.</param>
    /// <param name="ownerId">Identificador do responsável; nulo para escopo BU.</param>
    /// <param name="year">Ano do período.</param>
    /// <param name="month">Mês do período (1..12).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Aggregate <see cref="Goal"/> existente ou <c>null</c>.</returns>
    Task<Goal?> FindByKey(
        Guid tenantId,
        Guid buId,
        Guid? ownerId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca meta pelo identificador UUID.
    /// Retorna <c>null</c> quando o ID não existe no tenant.
    /// </summary>
    /// <param name="tenantId">Tenant do principal autenticado (INV-4).</param>
    /// <param name="id">Identificador único da meta.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Aggregate <see cref="Goal"/> existente ou <c>null</c>.</returns>
    Task<Goal?> FindById(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste um novo aggregate <see cref="Goal"/> na transação corrente.
    /// Domain events são despachados via outbox após o Add.
    /// </summary>
    /// <param name="goal">Aggregate a inserir.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task Add(Goal goal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste alterações no aggregate <see cref="Goal"/> existente.
    /// Domain events são despachados via outbox após o Update.
    /// </summary>
    /// <param name="goal">Aggregate com alterações a persistir.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task Update(Goal goal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista metas do tenant com filtros e paginação.
    /// Sempre respeitado o Global Query Filter de tenant_id.
    /// </summary>
    /// <param name="filter">Filtros e paginação.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Resultado paginado de aggregates <see cref="Goal"/>.</returns>
    Task<PagedResult<Goal>> Query(
        GoalQueryFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista todas as metas de um scope/ano para cálculo de agregação em memória.
    /// Usado por <c>GetGoalAggregateQueryHandler</c> (GoalAggregation, DD-003).
    /// </summary>
    /// <param name="tenantId">Tenant do principal autenticado.</param>
    /// <param name="buId">Identificador da BU.</param>
    /// <param name="ownerId">Identificador do responsável; nulo para escopo BU.</param>
    /// <param name="year">Ano de referência.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Lista de metas do período (≤ 12 itens).</returns>
    Task<IReadOnlyList<Goal>> ListByYear(
        Guid tenantId,
        Guid buId,
        Guid? ownerId,
        int year,
        CancellationToken cancellationToken = default);
}

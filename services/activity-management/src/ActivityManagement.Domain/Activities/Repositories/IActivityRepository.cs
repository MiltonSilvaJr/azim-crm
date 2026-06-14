namespace ActivityManagement.Domain.Activities.Repositories;

/// <summary>
/// Contrato do repositório do agregado <see cref="Activity"/>.
/// Definido no Domain para garantir a inversão de dependência (Clean Architecture, design §3).
/// A implementação concreta reside em <c>ActivityManagement.Infrastructure</c>.
/// Não expõe <see cref="System.Linq.IQueryable{T}"/> nem <c>DbSet</c> para fora da
/// camada de infraestrutura (rule clean-architecture.md §12).
/// Mapeia: design §4.1, §6.1, TASK-04.
/// </summary>
public interface IActivityRepository
{
    /// <summary>
    /// Recupera uma atividade pelo identificador dentro do tenant ativo.
    /// Retorna <c>null</c> quando não encontrada ou fora do tenant (anti-enumeração).
    /// </summary>
    /// <param name="id">Identificador UUID da atividade.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>O agregado <see cref="Activity"/> ou <c>null</c>.</returns>
    Task<Activity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste (insere ou atualiza) o agregado <see cref="Activity"/>.
    /// O <c>updated_at</c> é atualizado automaticamente pela camada de infraestrutura.
    /// </summary>
    /// <param name="activity">Agregado a persistir.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task SaveAsync(Activity activity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove o agregado do repositório (soft-delete ou hard-delete conforme a regra do projeto).
    /// </summary>
    /// <param name="id">Identificador UUID da atividade.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna as atividades vencidas não terminais de um tenant, em lote paginado.
    /// Usado pelo <c>ScanOverdueActivitiesCommand</c> (DD-005, design §5.1).
    /// </summary>
    /// <param name="referenceInstant">Instante de referência para comparação com <c>due_at</c>.</param>
    /// <param name="skip">Número de registros a pular (paginação).</param>
    /// <param name="take">Número de registros a retornar (tamanho do lote).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Lista paginada de atividades vencidas não terminais.</returns>
    Task<IReadOnlyList<Activity>> GetOverduePageAsync(
        DateTimeOffset referenceInstant,
        int            skip,
        int            take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna as atividades concluídas mais recentes para um conjunto de oportunidades.
    /// Suportado pelo índice <c>(tenant_id, opportunity_id, completed_at)</c> (RNF 4).
    /// </summary>
    /// <param name="opportunityIds">Lista de identificadores de oportunidades.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Mapa de <c>opportunityId → Activity</c> para a última atividade concluída de cada uma.</returns>
    Task<IReadOnlyDictionary<Guid, Activity>> GetLastCompletedByOpportunitiesAsync(
        IReadOnlyList<Guid> opportunityIds,
        CancellationToken   cancellationToken = default);
}

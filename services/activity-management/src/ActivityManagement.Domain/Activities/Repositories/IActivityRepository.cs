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

    /// <summary>
    /// Retorna atividades não terminais de um owner com <c>dueAt</c> dentro do intervalo fornecido.
    /// Usado por <c>GetMyDayQuery</c> e <c>GetMyWeekQuery</c> (Req 5, DD-008).
    /// </summary>
    /// <param name="ownerId">Identificador do usuário dono das atividades.</param>
    /// <param name="from">Início do intervalo (inclusivo).</param>
    /// <param name="to">Fim do intervalo (exclusivo).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Lista de atividades não terminais dentro do intervalo de datas.</returns>
    Task<IReadOnlyList<Activity>> GetByOwnerInRangeAsync(
        Guid              ownerId,
        DateTimeOffset    from,
        DateTimeOffset    to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna atividades não terminais vencidas de um owner (dueAt &lt; referenceInstant).
    /// Usado por <c>GetMyDayQuery</c> para a faixa "vencidas" (Req 5, Req 11.3).
    /// </summary>
    /// <param name="ownerId">Identificador do usuário dono das atividades.</param>
    /// <param name="referenceInstant">Instante de referência: atividades com dueAt anterior são consideradas vencidas.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Lista de atividades vencidas não terminais do owner.</returns>
    Task<IReadOnlyList<Activity>> GetOverdueByOwnerAsync(
        Guid              ownerId,
        DateTimeOffset    referenceInstant,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna página de atividades filtrada por critérios opcionais (Req 13, design §5.2).
    /// Não expõe <see cref="System.Linq.IQueryable{T}"/>; a infraestrutura aplica os filtros.
    /// </summary>
    /// <param name="filter">Critérios de filtro e paginação.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Página de atividades e total de registros.</returns>
    Task<(IReadOnlyList<Activity> Items, int Total)> ListAsync(
        ActivityListFilter filter,
        CancellationToken  cancellationToken = default);

    /// <summary>
    /// Retorna oportunidades abertas (IDs) que não possuem atividade não terminal com dueAt futuro
    /// vinculada à BU do usuário (Req 10, design §5.2).
    /// </summary>
    /// <param name="buId">Identificador da Business Unit.</param>
    /// <param name="openOpportunityIds">Lista de IDs de oportunidades abertas (da BU) para verificação.</param>
    /// <param name="referenceInstant">Instante de referência para "futuro".</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Subconjunto de <paramref name="openOpportunityIds"/> sem follow-up futuro.</returns>
    Task<IReadOnlyList<Guid>> GetOpportunityIdsWithoutFollowupAsync(
        Guid                buId,
        IReadOnlyList<Guid> openOpportunityIds,
        DateTimeOffset      referenceInstant,
        CancellationToken   cancellationToken = default);
}

/// <summary>
/// Filtro para <see cref="IActivityRepository.ListAsync"/> (Req 13, design §5.2).
/// Todos os campos são opcionais; campos nulos não são aplicados como filtro.
/// </summary>
/// <param name="OwnerId">Filtrar por dono da atividade (opcional).</param>
/// <param name="Type">Filtrar por tipo da atividade (opcional).</param>
/// <param name="Status">Filtrar por status (opcional).</param>
/// <param name="OnlyOverdue">Quando <c>true</c>, retorna apenas atividades vencidas não terminais.</param>
/// <param name="OpportunityId">Filtrar por oportunidade vinculada (opcional).</param>
/// <param name="Page">Número da página (base 1).</param>
/// <param name="PageSize">Tamanho da página (padrão: 20).</param>
public sealed record ActivityListFilter(
    Guid?   OwnerId       = null,
    string? Type          = null,
    string? Status        = null,
    bool    OnlyOverdue   = false,
    Guid?   OpportunityId = null,
    int     Page          = 1,
    int     PageSize      = 20);

using OpportunityPipeline.Domain.Opportunities;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Application.Opportunities.Queries;

// =========================================================================
// DTOs de leitura
// =========================================================================

/// <summary>Parâmetros de paginação padrão.</summary>
public sealed record PageRequest(int Page = 1, int PageSize = 20)
{
    public int Offset => (Page - 1) * PageSize;
}

/// <summary>Resultado paginado genérico.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

/// <summary>Filtros para listagem de oportunidades.</summary>
public sealed record OpportunityFilter(
    Guid? OwnerId = null,
    Guid? OriginChannelId = null,
    Guid? PartnerId = null,
    Guid? StageId = null,
    StageCategory? StageCategory = null,
    DateOnly? CreatedFrom = null,
    DateOnly? CreatedTo = null,
    bool? IsStale = null,
    string? SearchText = null);

/// <summary>Resumo de oportunidade para listagem e kanban.</summary>
public sealed record OpportunitySummary(
    Guid Id,
    string Number,
    string Title,
    Guid OwnerId,
    Guid AccountId,
    Guid? PartnerId,
    string StageName,
    StageCategory StageCategory,
    long TotalValueCents,
    long ForecastPonderadoCents,
    bool IsStale,
    bool IsOverdue,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Coluna do kanban com somas agregadas e cards paginados.</summary>
public sealed record KanbanColumn(
    Guid StageId,
    string StageName,
    int Order,
    long TotalValueCents,
    long ForecastPonderadoCents,
    int TotalCount,
    IReadOnlyList<OpportunitySummary> Cards);

/// <summary>Item de transição de estágio para timeline.</summary>
public sealed record TimelineEntry(
    Guid TransitionId,
    string? FromStageName,
    string ToStageName,
    string FromCategory,
    string ToCategory,
    DateTimeOffset OccurredAt,
    Guid ActorId);

/// <summary>Detalhe de comissão de parceiro para query.</summary>
public sealed record CommissionDetail(
    Guid PartnerId,
    string Role,
    decimal PctSetup,
    decimal PctRecorrente,
    long ValorFixoCents,
    int MesesComissionados,
    long ComissaoTotalCents,
    long ForecastLiquidoCents,
    bool IsSnapshot,
    DateTimeOffset? SnapshotAt);

// =========================================================================
// IOpportunityQueryRepository
// =========================================================================

/// <summary>
/// Porta de leitura especializada para queries de oportunidade.
/// Separada de IOpportunityRepository (CQRS — queries projetam DTOs sem carregar agregado).
/// Implementação na Infrastructure (Onda 4 — TASK-13).
/// Mapeia: design §5.2, TASK-12.
/// </summary>
public interface IOpportunityQueryRepository
{
    /// <summary>Carrega detalhe completo da oportunidade por ID (inclui transições e comissões).</summary>
    Task<Opportunity?> GetByIdWithDetailsAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Lista oportunidades com filtros e paginação.</summary>
    Task<PagedResult<OpportunitySummary>> ListAsync(
        Guid tenantId,
        Guid buId,
        OpportunityFilter filter,
        PageRequest page,
        CancellationToken cancellationToken = default);

    /// <summary>Retorna colunas do kanban com somas SQL e cards paginados por coluna.</summary>
    Task<IReadOnlyList<KanbanColumn>> GetKanbanAsync(
        Guid tenantId,
        Guid buId,
        int stagePageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Retorna a linha do tempo de transições da oportunidade.</summary>
    Task<IReadOnlyList<TimelineEntry>> GetTimelineAsync(
        Guid tenantId,
        Guid opportunityId,
        CancellationToken cancellationToken = default);

    /// <summary>Retorna detalhes de comissão para a oportunidade (projetada + snapshot).</summary>
    Task<IReadOnlyList<CommissionDetail>> GetCommissionsAsync(
        Guid tenantId,
        Guid opportunityId,
        CancellationToken cancellationToken = default);

    /// <summary>Lista IDs de oportunidades abertas por tenant/BU para detecção de estagnação.</summary>
    Task<IReadOnlyList<Guid>> ListOpenOpportunityIdsAsync(
        Guid tenantId,
        Guid buId,
        CancellationToken cancellationToken = default);

    /// <summary>Retorna oportunidades marcadas como estagnadas.</summary>
    Task<PagedResult<OpportunitySummary>> ListStaleAsync(
        Guid tenantId,
        Guid buId,
        PageRequest page,
        CancellationToken cancellationToken = default);

    /// <summary>Retorna forecast agregado para o tenant/BU.</summary>
    Task<(long TotalValueCents, long ForecastPonderadoCents, long ForecastLiquidoCents)> GetForecastAsync(
        Guid tenantId,
        Guid buId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Porta de repositório para registros de detecção de estagnação (idempotência).
/// PK: (tenant_id, opportunity_id, detection_period).
/// Implementação na Infrastructure (Onda 4 — TASK-18).
/// Mapeia: Req 17, RNF 9, PBT-09, design §5.3, TASK-12.
/// </summary>
public interface IStaleDetectionRunRepository
{
    /// <summary>Verifica se já existe run para (opportunity_id, detection_period).</summary>
    Task<bool> ExistsAsync(
        Guid tenantId,
        Guid opportunityId,
        string detectionPeriod,
        CancellationToken cancellationToken = default);

    /// <summary>Registra execução de detecção de estagnação (idempotente por PK).</summary>
    Task RegisterAsync(
        Guid tenantId,
        Guid opportunityId,
        string detectionPeriod,
        DateTimeOffset detectedAt,
        CancellationToken cancellationToken = default);
}

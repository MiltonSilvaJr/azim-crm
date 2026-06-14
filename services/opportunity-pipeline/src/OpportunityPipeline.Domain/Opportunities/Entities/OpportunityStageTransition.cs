using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Opportunities.Entities;

/// <summary>
/// Registro imutável de cada mudança de estágio da oportunidade (append-only).
/// Todos os campos init-only. Nunca mutável após inserção (RNF 7).
/// Forma a linha do tempo da oportunidade (Req 5.3, Req 19.3).
/// Mapeia: Req 5, RNF 7, design §4.2.
/// </summary>
public sealed class OpportunityStageTransition
{
    /// <summary>Identificador único da transição.</summary>
    public Guid Id { get; init; }

    /// <summary>Tenant ao qual pertence (multi-tenancy, ADR-0001).</summary>
    public Guid TenantId { get; init; }

    /// <summary>Oportunidade à qual pertence.</summary>
    public Guid OpportunityId { get; init; }

    /// <summary>Estágio de origem (nulo na criação inicial).</summary>
    public Guid? FromStageId { get; init; }

    /// <summary>Estágio de destino.</summary>
    public Guid ToStageId { get; init; }

    /// <summary>Categoria de origem (nula na criação).</summary>
    public StageCategory? FromCategory { get; init; }

    /// <summary>Categoria de destino.</summary>
    public StageCategory ToCategory { get; init; }

    /// <summary>Usuário que realizou a transição.</summary>
    public Guid ActorId { get; init; }

    /// <summary>Data/hora em que a transição ocorreu.</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>Inicializa todos os campos (imutável após construção).</summary>
    public OpportunityStageTransition(
        Guid id,
        Guid tenantId,
        Guid opportunityId,
        Guid? fromStageId,
        Guid toStageId,
        StageCategory? fromCategory,
        StageCategory toCategory,
        Guid actorId,
        DateTimeOffset occurredAt)
    {
        Id = id;
        TenantId = tenantId;
        OpportunityId = opportunityId;
        FromStageId = fromStageId;
        ToStageId = toStageId;
        FromCategory = fromCategory;
        ToCategory = toCategory;
        ActorId = actorId;
        OccurredAt = occurredAt;
    }
}

namespace OpportunityPipeline.Infrastructure.Persistence;

/// <summary>
/// Contador atômico de numeração de oportunidade por tenant.
/// Tabela: opportunity_number_sequences (DD-001, PBT-01).
/// </summary>
public sealed class OpportunityNumberSequence
{
    /// <summary>Tenant dono do contador.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Próximo valor a ser consumido (incrementado atomicamente).</summary>
    public long NextValue { get; set; }
}

/// <summary>
/// Registro de execução de detecção de estagnação.
/// PK: (tenant_id, opportunity_id, detection_period) — garante idempotência (RNF 9, PBT-09).
/// </summary>
public sealed class StaleDetectionRun
{
    public Guid TenantId { get; set; }
    public Guid OpportunityId { get; set; }

    /// <summary>Janela de detecção em formato "YYYY-MM-DD".</summary>
    public string DetectionPeriod { get; set; } = string.Empty;

    public DateTimeOffset DetectedAt { get; set; }
}

/// <summary>
/// Filtro salvo pelo usuário para lista de oportunidades.
/// </summary>
public sealed class SavedFilterEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Critérios serializados em JSON.</summary>
    public string CriteriaJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

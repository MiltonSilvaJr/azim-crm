namespace GoalForecast.Contracts;

/// <summary>
/// Schema do evento de integração <c>goal.updated.v1</c> publicado no topic <c>azim-goals</c>.
/// Imutável (record). Todos os valores monetários em centavos inteiros (long) — nunca double.
///
/// Compatibilidade retroativa por versionamento de sufixo (.v1).
/// Novos campos opcionais não quebram consumidores (design §9.1).
///
/// Headers esperados: correlation_id, causation_id, tenant_id, event_version=1.
/// Consumidores: audit-log (append-only), digest, reporting.
///
/// Mapeia: design §9.1, AsyncAPI §9.1, RNF 5, TASK-21, TASK-26.
/// </summary>
public sealed record GoalUpdatedEvent
{
    /// <summary>
    /// Identificador único do evento para deduplicação/idempotência.
    /// </summary>
    public required Guid EventId { get; init; }

    /// <summary>Tenant ao qual a meta pertence (isolamento, RNF 1).</summary>
    public required Guid TenantId { get; init; }

    /// <summary>Identidade do aggregate Goal.</summary>
    public required Guid GoalId { get; init; }

    /// <summary>Unidade de negócio da meta.</summary>
    public required Guid BuId { get; init; }

    /// <summary>Responsável da meta; nulo no escopo BU.</summary>
    public Guid? OwnerId { get; init; }

    /// <summary>Ano do período da meta.</summary>
    public required int Year { get; init; }

    /// <summary>Mês do período da meta (1..12).</summary>
    public required int Month { get; init; }

    /// <summary>
    /// Ação que originou o evento: "created" ou "updated".
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// ValorMeta anterior em centavos inteiros (long).
    /// Nulo quando Action = "created" (design §9.1).
    /// Nunca double — risco RISK-GOAL-05.
    /// </summary>
    public long? ValorMetaAnterior { get; init; }

    /// <summary>
    /// ValorMeta novo em centavos inteiros (long).
    /// Nunca double — risco RISK-GOAL-05.
    /// </summary>
    public required long ValorMetaNovo { get; init; }

    /// <summary>Timestamp UTC do evento.</summary>
    public required DateTimeOffset OccurredAt { get; init; }
}

using GoalForecast.Domain.Common;

namespace GoalForecast.Domain.Events;

/// <summary>
/// Domain event emitido após <c>Goal.Create</c> ou <c>Goal.ChangeValorMeta</c>.
/// Payload canônico do evento de integração <c>goal.updated.v1</c>.
/// Imutável (record). Todos os valores monetários em centavos inteiros (long).
///
/// Campos conforme design §4.4:
/// <list type="bullet">
///   <item><term>goalId</term><description>Identidade do agregado.</description></item>
///   <item><term>tenantId</term><description>Isolamento multi-tenant (RNF 1).</description></item>
///   <item><term>buId</term><description>Unidade de negócio.</description></item>
///   <item><term>ownerId</term><description>Responsável; nulo no escopo BU.</description></item>
///   <item><term>year / month</term><description>Período da meta.</description></item>
///   <item><term>action</term><description>created ou updated.</description></item>
///   <item><term>valorMetaAnterior</term><description>Nulo em created; centavos em updated.</description></item>
///   <item><term>valorMetaNovo</term><description>Valor atual em centavos.</description></item>
///   <item><term>occurredAt</term><description>Timestamp UTC do evento.</description></item>
/// </list>
///
/// Mapeia: Req 10, RNF 5, design §4.4, AsyncAPI §9.1, TASK-05, TASK-06.
/// </summary>
public sealed record GoalUpdated : IDomainEvent
{
    /// <inheritdoc/>
    public Guid EventId { get; }

    /// <inheritdoc/>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>Identidade do aggregate Goal.</summary>
    public Guid GoalId { get; }

    /// <summary>Tenant ao qual a meta pertence.</summary>
    public Guid TenantId { get; }

    /// <summary>Unidade de negócio da meta.</summary>
    public Guid BuId { get; }

    /// <summary>Responsável da meta; nulo no escopo BU.</summary>
    public Guid? OwnerId { get; }

    /// <summary>Ano do período da meta.</summary>
    public int Year { get; }

    /// <summary>Mês do período da meta (1..12).</summary>
    public int Month { get; }

    /// <summary>Ação que originou o evento: created ou updated.</summary>
    public GoalUpdatedAction Action { get; }

    /// <summary>
    /// ValorMeta anterior em centavos inteiros.
    /// Nulo quando <see cref="Action"/> == Created.
    /// </summary>
    public long? ValorMetaAnterior { get; }

    /// <summary>ValorMeta novo em centavos inteiros.</summary>
    public long ValorMetaNovo { get; }

    /// <summary>
    /// Cria um evento GoalUpdated com o payload completo.
    /// </summary>
    public GoalUpdated(
        Guid goalId,
        Guid tenantId,
        Guid buId,
        Guid? ownerId,
        int year,
        int month,
        GoalUpdatedAction action,
        long? valorMetaAnterior,
        long valorMetaNovo,
        DateTimeOffset occurredAt)
    {
        EventId = Guid.NewGuid();
        GoalId = goalId;
        TenantId = tenantId;
        BuId = buId;
        OwnerId = ownerId;
        Year = year;
        Month = month;
        Action = action;
        ValorMetaAnterior = valorMetaAnterior;
        ValorMetaNovo = valorMetaNovo;
        OccurredAt = occurredAt;
    }
}

using GoalForecast.Domain.Common;
using GoalForecast.Domain.Events;
using GoalForecast.Domain.Exceptions;
using GoalForecast.Domain.ValueObjects;

namespace GoalForecast.Domain.Aggregates;

/// <summary>
/// Aggregate Root único do módulo goal-forecast.
/// Representa a meta mensal de uma unidade de negócio ou responsável em um período.
///
/// Invariantes protegidas:
/// <list type="bullet">
///   <item><term>INV-1</term><description>ValorMeta >= 0 em centavos inteiros (garantida por Money).</description></item>
///   <item><term>INV-2</term><description>Period.Month ∈ [1..12] e Year de quatro dígitos (garantida por GoalPeriod).</description></item>
///   <item><term>INV-3</term><description>Scope.BuId obrigatório; OwnerId condicional (garantida por GoalScope).</description></item>
///   <item><term>INV-4</term><description>TenantId imutável após criação; não pode ser Guid.Empty.</description></item>
/// </list>
///
/// Mapeia: Req 1, Req 4, INV-1..4, design §4.1, TASK-05.
/// </summary>
public sealed class Goal
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Identidade do aggregate (UUID).</summary>
    public Guid Id { get; }

    /// <summary>Tenant ao qual a meta pertence. Imutável após criação (INV-4).</summary>
    public Guid TenantId { get; }

    /// <summary>Escopo da meta: BU ou RESPONSAVEL.</summary>
    public GoalScope Scope { get; }

    /// <summary>Período da meta (ano + mês).</summary>
    public GoalPeriod Period { get; }

    /// <summary>Valor da meta em centavos inteiros. Não-negativo (INV-1).</summary>
    public Money ValorMeta { get; private set; }

    /// <summary>Momento de criação do registro (UTC).</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Momento da última atualização (UTC). Atualizado por ChangeValorMeta.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Coleção de domain events acumulados para despacho posterior via outbox.
    /// Somente leitura externamente; limpa via <see cref="ClearDomainEvents"/>.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private Goal(Guid id, Guid tenantId, GoalScope scope, GoalPeriod period, Money valorMeta, DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        Scope = scope;
        Period = period;
        ValorMeta = valorMeta;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// Factory que cria um novo aggregate Goal validando todas as invariantes INV-1..4.
    /// Emite <see cref="GoalUpdated"/> com <c>action=created</c>.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant. Não pode ser <see cref="Guid.Empty"/> (INV-4).</param>
    /// <param name="scope">Escopo da meta (INV-3, validado por GoalScope).</param>
    /// <param name="period">Período da meta (INV-2, validado por GoalPeriod).</param>
    /// <param name="valorMeta">Valor em centavos inteiros não-negativo (INV-1, validado por Money).</param>
    /// <returns>Novo aggregate Goal com evento de criação acumulado.</returns>
    /// <exception cref="DomainException">GF-ERR-003 quando tenantId é vazio (INV-4).</exception>
    public static Goal Create(Guid tenantId, GoalScope scope, GoalPeriod period, Money valorMeta)
    {
        // INV-4: TenantId obrigatório e imutável.
        if (tenantId == Guid.Empty)
            throw new DomainException("GF-ERR-003",
                "TenantId é obrigatório e não pode ser Guid.Empty (INV-4).");

        // INV-1: garantida por Money.Of/constructor (valorMeta.Cents >= 0).
        // INV-2: garantida por GoalPeriod.
        // INV-3: garantida por GoalScope.

        var now = DateTimeOffset.UtcNow;
        var goal = new Goal(Guid.NewGuid(), tenantId, scope, period, valorMeta, now);

        goal._domainEvents.Add(new GoalUpdated(
            goalId: goal.Id,
            tenantId: tenantId,
            buId: scope.BuId,
            ownerId: scope.OwnerId,
            year: period.Year,
            month: period.Month,
            action: GoalUpdatedAction.Created,
            valorMetaAnterior: null,
            valorMetaNovo: valorMeta.Cents,
            occurredAt: now));

        return goal;
    }

    /// <summary>
    /// Atualiza o valor da meta. Revalida INV-1, atualiza UpdatedAt e emite
    /// <see cref="GoalUpdated"/> com <c>action=updated</c> e delta (anterior/novo).
    /// </summary>
    /// <param name="novoValor">Novo valor em centavos inteiros não-negativo (INV-1).</param>
    /// <exception cref="DomainException">GF-ERR-001 quando novoValor contém centavos negativos.</exception>
    public void ChangeValorMeta(Money novoValor)
    {
        // INV-1: garantida por Money (novoValor.Cents >= 0 por construção).
        var valorAnterior = ValorMeta;
        var now = DateTimeOffset.UtcNow;

        ValorMeta = novoValor;
        UpdatedAt = now;

        _domainEvents.Add(new GoalUpdated(
            goalId: Id,
            tenantId: TenantId,
            buId: Scope.BuId,
            ownerId: Scope.OwnerId,
            year: Period.Year,
            month: Period.Month,
            action: GoalUpdatedAction.Updated,
            valorMetaAnterior: valorAnterior.Cents,
            valorMetaNovo: novoValor.Cents,
            occurredAt: now));
    }

    /// <summary>
    /// Limpa a coleção de domain events após despacho via outbox.
    /// Chamado pelo repositório/outbox após persistência bem-sucedida.
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}

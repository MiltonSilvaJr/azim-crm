using FluentAssertions;
using GoalForecast.Domain.Aggregates;
using GoalForecast.Domain.Events;
using GoalForecast.Domain.Exceptions;
using GoalForecast.Domain.ValueObjects;
using Xunit;

namespace GoalForecast.Domain.Tests.Aggregates;

/// <summary>
/// Testes do Aggregate Root <see cref="Goal"/>.
/// Cobre TASK-05: INV-1..4, Goal.Create, ChangeValorMeta e acúmulo de domain events.
/// Mapeia: Req 1, Req 4, INV-1..4, design §4.1, TASK-05.
/// </summary>
public sealed class GoalTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly GoalScope ScopeBu = GoalScope.ForBu(Guid.NewGuid());
    private static readonly GoalScope ScopeResp = GoalScope.ForResponsavel(Guid.NewGuid(), Guid.NewGuid());
    private static readonly GoalPeriod Period = new(2026, 6);
    private static readonly Money ValorMeta = Money.Of(50_000_00L); // R$ 50.000,00

    // =========================================================================
    // Goal.Create — criação válida
    // =========================================================================

    [Fact(DisplayName = "Goal.Create com dados válidos cria aggregate com todos os campos")]
    public void Create_ValidData_CreatesAggregate()
    {
        var goal = Goal.Create(TenantId, ScopeBu, Period, ValorMeta);

        goal.Id.Should().NotBe(Guid.Empty);
        goal.TenantId.Should().Be(TenantId);
        goal.Scope.Should().Be(ScopeBu);
        goal.Period.Should().Be(Period);
        goal.ValorMeta.Should().Be(ValorMeta);
        goal.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, precision: TimeSpan.FromSeconds(5));
        goal.UpdatedAt.Should().Be(goal.CreatedAt);
    }

    [Fact(DisplayName = "Goal.Create acumula GoalUpdated com action=created")]
    public void Create_ValidData_AccumulatesGoalUpdatedCreatedEvent()
    {
        var goal = Goal.Create(TenantId, ScopeBu, Period, ValorMeta);

        var events = goal.DomainEvents;
        events.Should().HaveCount(1);

        var evt = events[0].Should().BeOfType<GoalUpdated>().Subject;
        evt.Action.Should().Be(GoalUpdatedAction.Created);
        evt.GoalId.Should().Be(goal.Id);
        evt.TenantId.Should().Be(TenantId);
        evt.ValorMetaAnterior.Should().BeNull();
        evt.ValorMetaNovo.Should().Be(ValorMeta.Cents);
    }

    // =========================================================================
    // INV-1: ValorMeta deve ser não-negativo
    // =========================================================================

    [Fact(DisplayName = "INV-1: Goal.Create com valor negativo lança DomainException")]
    public void Create_NegativeValorMeta_ThrowsDomainException()
    {
        // Money.Of já garante INV-1, então testamos que o aggregate não aceita Money inválido.
        // Money.Of(-1) lança DomainException GF-ERR-001 antes do Create.
        var act = () => Money.Of(-1L);

        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("GF-ERR-001");
    }

    [Fact(DisplayName = "INV-1: Goal.Create com Money.Zero é válido (valor meta = 0 é permitido)")]
    public void Create_WithMoneyZero_IsValid()
    {
        var act = () => Goal.Create(TenantId, ScopeBu, Period, Money.Zero);

        act.Should().NotThrow();
    }

    // =========================================================================
    // INV-2: Período válido (delegado a GoalPeriod)
    // =========================================================================

    [Fact(DisplayName = "INV-2: Goal.Create com mês inválido lança (via GoalPeriod)")]
    public void Create_InvalidMonth_ThrowsDomainException()
    {
        var act = () => new GoalPeriod(2026, 13);

        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("GF-ERR-002");
    }

    // =========================================================================
    // INV-3: Escopo coerente (delegado a GoalScope)
    // =========================================================================

    [Fact(DisplayName = "INV-3: Goal.Create com BuId vazio lança (via GoalScope)")]
    public void Create_EmptyBuId_ThrowsDomainException()
    {
        var act = () => GoalScope.ForBu(Guid.Empty);

        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("GF-ERR-003");
    }

    // =========================================================================
    // INV-4: TenantId é imutável após criação
    // =========================================================================

    [Fact(DisplayName = "INV-4: Goal.Create com TenantId vazio lança DomainException")]
    public void Create_EmptyTenantId_ThrowsDomainException()
    {
        var act = () => Goal.Create(Guid.Empty, ScopeBu, Period, ValorMeta);

        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("GF-ERR-003");
    }

    [Fact(DisplayName = "INV-4: TenantId é imutável após criação")]
    public void TenantId_IsImmutableAfterCreate()
    {
        var goal = Goal.Create(TenantId, ScopeBu, Period, ValorMeta);
        var originalTenantId = goal.TenantId;

        // Mesmo após ChangeValorMeta, TenantId não muda.
        goal.ChangeValorMeta(Money.Of(100L));

        goal.TenantId.Should().Be(originalTenantId);
    }

    // =========================================================================
    // ChangeValorMeta — valor válido
    // =========================================================================

    [Fact(DisplayName = "ChangeValorMeta com valor válido atualiza ValorMeta")]
    public void ChangeValorMeta_ValidValue_UpdatesValorMeta()
    {
        var goal = Goal.Create(TenantId, ScopeBu, Period, ValorMeta);
        goal.ClearDomainEvents();
        var novoValor = Money.Of(60_000_00L);

        goal.ChangeValorMeta(novoValor);

        goal.ValorMeta.Should().Be(novoValor);
    }

    [Fact(DisplayName = "ChangeValorMeta atualiza UpdatedAt")]
    public void ChangeValorMeta_ValidValue_UpdatesUpdatedAt()
    {
        var goal = Goal.Create(TenantId, ScopeBu, Period, ValorMeta);
        var createdAt = goal.CreatedAt;

        goal.ChangeValorMeta(Money.Of(60_000_00L));

        goal.UpdatedAt.Should().BeOnOrAfter(createdAt);
    }

    [Fact(DisplayName = "ChangeValorMeta acumula GoalUpdated com action=updated e delta correto")]
    public void ChangeValorMeta_ValidValue_AccumulatesGoalUpdatedEvent()
    {
        var goal = Goal.Create(TenantId, ScopeBu, Period, ValorMeta);
        goal.ClearDomainEvents();
        var novoValor = Money.Of(60_000_00L);

        goal.ChangeValorMeta(novoValor);

        var events = goal.DomainEvents;
        events.Should().HaveCount(1);

        var evt = events[0].Should().BeOfType<GoalUpdated>().Subject;
        evt.Action.Should().Be(GoalUpdatedAction.Updated);
        evt.GoalId.Should().Be(goal.Id);
        evt.TenantId.Should().Be(TenantId);
        evt.ValorMetaAnterior.Should().Be(ValorMeta.Cents);
        evt.ValorMetaNovo.Should().Be(novoValor.Cents);
    }

    [Fact(DisplayName = "ChangeValorMeta com Money.Zero é válido (meta zerada é permitida)")]
    public void ChangeValorMeta_WithZero_IsValid()
    {
        var goal = Goal.Create(TenantId, ScopeBu, Period, ValorMeta);

        var act = () => goal.ChangeValorMeta(Money.Zero);

        act.Should().NotThrow();
        goal.ValorMeta.Should().Be(Money.Zero);
    }

    // =========================================================================
    // Domain events — coleção e limpeza
    // =========================================================================

    [Fact(DisplayName = "DomainEvents está inicialmente vazio antes de Create acumular")]
    public void DomainEvents_AfterClear_IsEmpty()
    {
        var goal = Goal.Create(TenantId, ScopeBu, Period, ValorMeta);
        goal.ClearDomainEvents();

        goal.DomainEvents.Should().BeEmpty();
    }

    [Fact(DisplayName = "ClearDomainEvents remove todos os eventos acumulados")]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var goal = Goal.Create(TenantId, ScopeBu, Period, ValorMeta);
        goal.ChangeValorMeta(Money.Of(100L));

        goal.ClearDomainEvents();

        goal.DomainEvents.Should().BeEmpty();
    }

    [Fact(DisplayName = "Múltiplos ChangeValorMeta acumulam um evento por chamada")]
    public void MultipleChanges_AccumulateOneEventEach()
    {
        var goal = Goal.Create(TenantId, ScopeBu, Period, ValorMeta);
        goal.ClearDomainEvents();

        goal.ChangeValorMeta(Money.Of(100L));
        goal.ChangeValorMeta(Money.Of(200L));

        goal.DomainEvents.Should().HaveCount(2);
    }

    // =========================================================================
    // GoalUpdated em Create — campos do payload canônico
    // =========================================================================

    [Fact(DisplayName = "GoalUpdated de Create tem BuId e OwnerId corretos")]
    public void Create_Event_HasCorrectBuIdAndOwnerId()
    {
        var buId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var scope = GoalScope.ForResponsavel(buId, ownerId);
        var goal = Goal.Create(TenantId, scope, Period, ValorMeta);

        var evt = (GoalUpdated)goal.DomainEvents[0];
        evt.BuId.Should().Be(buId);
        evt.OwnerId.Should().Be(ownerId);
    }

    [Fact(DisplayName = "GoalUpdated de Create em escopo BU tem OwnerId nulo")]
    public void Create_BuScope_EventHasNullOwnerId()
    {
        var goal = Goal.Create(TenantId, ScopeBu, Period, ValorMeta);

        var evt = (GoalUpdated)goal.DomainEvents[0];
        evt.OwnerId.Should().BeNull();
    }
}

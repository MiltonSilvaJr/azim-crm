using OpportunityPipeline.Domain.Opportunities;
using OpportunityPipeline.Domain.Opportunities.Events;
using OpportunityPipeline.Domain.Opportunities.Exceptions;
using OpportunityPipeline.Domain.Opportunities.Services;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Tests.Opportunities.Aggregate;

/// <summary>
/// Testes para Opportunity.MoveStage + OpportunityLifecycle.
/// PBT-02 (imutabilidade number) e PBT-08 (state machine).
/// Mapeia: Req 5, Req 6, INV-6, PBT-02, PBT-08, TASK-06.
/// </summary>
public sealed class OpportunityMoveStageTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BuId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private const int PropostaEnviadaOrder = 3;

    private static Opportunity CreateOpenOpportunity(string number = "AZ-0001")
    {
        return Opportunity.Create(
            tenantId: TenantId,
            buId: BuId,
            accountId: Guid.NewGuid(),
            ownerId: Guid.NewGuid(),
            partnerId: null,
            stage: new StageRef(Guid.NewGuid(), "Qualificação", StageCategory.Open, 20, 1),
            originChannel: new OriginChannelRef(Guid.NewGuid(), "Direto", IsPartnerChannel: false),
            title: "Oportunidade Teste",
            contractValue: new ContractValue(new Money(10000L), Money.Zero, 0),
            probability: new Probability(20),
            expectedCloseDate: null,
            notes: null,
            number: new OpportunityNumber(number),
            createdBy: Guid.NewGuid(),
            now: Now);
    }

    // =========================================================================
    // MoveStage válido
    // =========================================================================

    [Fact(DisplayName = "MoveStage: open → open válido, emite OpportunityStageChanged")]
    public void MoveStage_OpenToOpen_EmitsStageChanged()
    {
        var opp = CreateOpenOpportunity();
        opp.ClearDomainEvents();

        var newStage = new StageRef(Guid.NewGuid(), "Proposta", StageCategory.Open, 40, 2);
        opp.MoveStage(newStage, ActorId, Now, PropostaEnviadaOrder);

        opp.Stage.Should().Be(newStage);
        opp.StageCategory.Should().Be(StageCategory.Open);
        opp.DomainEvents.Should().HaveCount(1);
        opp.DomainEvents[0].Should().BeOfType<OpportunityStageChanged>();
    }

    [Fact(DisplayName = "MoveStage: adiciona transição na timeline")]
    public void MoveStage_AddsTransition()
    {
        var opp = CreateOpenOpportunity();
        var previousTransitions = opp.Transitions.Count;

        var newStage = new StageRef(Guid.NewGuid(), "Proposta", StageCategory.Open, 40, 2);
        opp.MoveStage(newStage, ActorId, Now, PropostaEnviadaOrder);

        opp.Transitions.Should().HaveCount(previousTransitions + 1);
    }

    [Fact(DisplayName = "MoveStage: INV-6 — estágio ≥ Proposta Enviada sem data lança ExpectedCloseDateRequiredException")]
    public void MoveStage_WithoutExpectedDateForPropostaEnviada_ThrowsException()
    {
        var opp = CreateOpenOpportunity();
        // Estágio com order = 3 (Proposta Enviada) sem expected_close_date
        var stageAtOrAfterProposta = new StageRef(Guid.NewGuid(), "Proposta Enviada", StageCategory.Open, 60, 3);

        var act = () => opp.MoveStage(stageAtOrAfterProposta, ActorId, Now, PropostaEnviadaOrder);
        act.Should().Throw<ExpectedCloseDateRequiredException>();
    }

    [Fact(DisplayName = "MoveStage: estágio < Proposta Enviada sem data não lança exceção")]
    public void MoveStage_StageBeforePropostaWithoutDate_DoesNotThrow()
    {
        var opp = CreateOpenOpportunity();
        var earlyStage = new StageRef(Guid.NewGuid(), "Prospecção", StageCategory.Open, 10, 2);

        var act = () => opp.MoveStage(earlyStage, ActorId, Now, PropostaEnviadaOrder);
        act.Should().NotThrow();
    }

    // =========================================================================
    // Transições inválidas — sem efeito colateral (PBT-08)
    // =========================================================================

    [Fact(DisplayName = "MoveStage: won → lost lança InvalidStageTransitionException sem efeito colateral")]
    public void MoveStage_WonToLost_ThrowsWithoutSideEffect()
    {
        var opp = CreateOpenOpportunity();
        opp.Win(ActorId, Now); // open → won
        var transitionsBeforeInvalid = opp.Transitions.Count;
        var eventsBeforeInvalid = opp.DomainEvents.Count;

        var lostStage = new StageRef(Guid.NewGuid(), "Perdida", StageCategory.Lost, 0, 5);
        var act = () => opp.MoveStage(lostStage, ActorId, Now, PropostaEnviadaOrder);

        act.Should().Throw<InvalidStageTransitionException>();
        // Sem efeito colateral: transições e eventos não foram adicionados
        opp.Transitions.Should().HaveCount(transitionsBeforeInvalid);
        opp.DomainEvents.Should().HaveCount(eventsBeforeInvalid);
        opp.StageCategory.Should().Be(StageCategory.Won);
    }

    [Fact(DisplayName = "OpportunityLifecycle: won → lost rejeitado")]
    public void Lifecycle_WonToLost_Rejected()
    {
        var act = () => OpportunityLifecycle.ValidateTransition(
            StageCategory.Won, StageCategory.Lost);
        act.Should().Throw<InvalidStageTransitionException>();
    }

    [Fact(DisplayName = "OpportunityLifecycle: lost → won rejeitado")]
    public void Lifecycle_LostToWon_Rejected()
    {
        var act = () => OpportunityLifecycle.ValidateTransition(
            StageCategory.Lost, StageCategory.Won);
        act.Should().Throw<InvalidStageTransitionException>();
    }

    [Fact(DisplayName = "OpportunityLifecycle: open → won aceito")]
    public void Lifecycle_OpenToWon_Accepted()
    {
        var act = () => OpportunityLifecycle.ValidateTransition(
            StageCategory.Open, StageCategory.Won);
        act.Should().NotThrow();
    }

    // =========================================================================
    // PBT-02: imutabilidade do opportunity_number
    // =========================================================================

    [Property(MaxTest = 200, DisplayName = "PBT-02: opportunity_number imutável sob qualquer sequência de MoveStage")]
    public Property PBT02_OpportunityNumber_ImmutableUnderMoveStage()
    {
        // Gera sequências de estágios open válidos para aplicar
        var stageSequenceGen = Gen.Choose(0, 5).Select(n =>
            Enumerable.Range(0, n).Select(i =>
                new StageRef(Guid.NewGuid(), $"Estágio {i}", StageCategory.Open, i * 10, i + 1)).ToList());

        return Prop.ForAll(
            Arb.From(stageSequenceGen),
            stages =>
            {
                var opp = CreateOpenOpportunity("AZ-1234");
                var originalNumber = opp.Number.Value;

                foreach (var stage in stages)
                {
                    try
                    {
                        opp.MoveStage(stage, ActorId, Now, PropostaEnviadaOrder);
                    }
                    catch (ExpectedCloseDateRequiredException)
                    {
                        // Ignoramos a exceção de data — o número ainda deve estar imutável
                    }
                }

                return opp.Number.Value == originalNumber;
            });
    }

    // =========================================================================
    // PBT-08: máquina de estados — transições inválidas sem efeito colateral
    // =========================================================================

    [Property(MaxTest = 200, DisplayName = "PBT-08: transições inválidas rejeitadas sem efeito colateral")]
    public Property PBT08_InvalidTransitions_RejectedWithoutSideEffect()
    {
        // Categorias para testar transições inválidas
        var invalidTransitions = new (StageCategory From, StageCategory To)[]
        {
            (StageCategory.Won, StageCategory.Lost),
            (StageCategory.Lost, StageCategory.Won),
        };

        return Prop.ForAll(
            Arb.From(Gen.Choose(0, invalidTransitions.Length - 1)),
            idx =>
            {
                var (from, to) = invalidTransitions[idx];
                try
                {
                    OpportunityLifecycle.ValidateTransition(from, to);
                    return false; // deve ter lançado
                }
                catch (InvalidStageTransitionException)
                {
                    return true;
                }
            });
    }
}

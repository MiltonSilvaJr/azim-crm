using OpportunityPipeline.Domain.Opportunities;
using OpportunityPipeline.Domain.Opportunities.Events;
using OpportunityPipeline.Domain.Opportunities.Exceptions;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Tests.Opportunities.Aggregate;

/// <summary>
/// Testes para Opportunity.Create — invariantes INV-1..5, INV-8, INV-9.
/// Mapeia: Req 1, Req 2, Req 4, Req 5, INV-1..5, TASK-06.
/// </summary>
public sealed class OpportunityCreateTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BuId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static StageRef DefaultStage() =>
        new(Guid.NewGuid(), "Qualificação", StageCategory.Open, 20, 1);

    private static OriginChannelRef DirectChannel() =>
        new(Guid.NewGuid(), "Direto", IsPartnerChannel: false);

    private static OriginChannelRef PartnerChannel() =>
        new(Guid.NewGuid(), "Parceiro", IsPartnerChannel: true);

    private static ContractValue DefaultContractValue() =>
        new(new Money(10000L, "BRL"), Money.Zero("BRL"), 0);

    private static Probability DefaultProbability() => new(20);

    private static OpportunityNumber DefaultNumber() =>
        new("AZ-0001");

    [Fact(DisplayName = "Create: criação válida retorna oportunidade com stage_category = open")]
    public void Create_Valid_ReturnsOpenOpportunity()
    {
        var opp = Opportunity.Create(
            tenantId: TenantId,
            buId: BuId,
            accountId: AccountId,
            ownerId: OwnerId,
            partnerId: null,
            stage: DefaultStage(),
            originChannel: DirectChannel(),
            title: "Oportunidade Teste",
            contractValue: DefaultContractValue(),
            probability: DefaultProbability(),
            expectedCloseDate: null,
            notes: null,
            number: DefaultNumber(),
            createdBy: CreatedBy,
            now: Now);

        opp.StageCategory.Should().Be(StageCategory.Open);
        opp.TenantId.Should().Be(TenantId);
        opp.OwnerId.Should().Be(OwnerId);
    }

    [Fact(DisplayName = "Create: emite exatamente 1 OpportunityCreated")]
    public void Create_Valid_EmitsOneOpportunityCreated()
    {
        var opp = Opportunity.Create(
            TenantId, BuId, AccountId, OwnerId, null,
            DefaultStage(), DirectChannel(), "Teste",
            DefaultContractValue(), DefaultProbability(), null, null,
            DefaultNumber(), CreatedBy, Now);

        opp.DomainEvents.Should().HaveCount(1);
        opp.DomainEvents[0].Should().BeOfType<OpportunityCreated>();
    }

    [Fact(DisplayName = "Create: registra exatamente 1 OpportunityStageTransition")]
    public void Create_Valid_RegistersOneTransition()
    {
        var opp = Opportunity.Create(
            TenantId, BuId, AccountId, OwnerId, null,
            DefaultStage(), DirectChannel(), "Teste",
            DefaultContractValue(), DefaultProbability(), null, null,
            DefaultNumber(), CreatedBy, Now);

        opp.Transitions.Should().HaveCount(1);
        opp.Transitions[0].FromStageId.Should().BeNull();
        opp.Transitions[0].ToCategory.Should().Be(StageCategory.Open);
    }

    [Fact(DisplayName = "Create: INV-1 — owner_id vazio lança OwnerRequiredException")]
    public void Create_EmptyOwner_ThrowsOwnerRequiredException()
    {
        var act = () => Opportunity.Create(
            TenantId, BuId, AccountId, Guid.Empty, null,
            DefaultStage(), DirectChannel(), "Teste",
            DefaultContractValue(), DefaultProbability(), null, null,
            DefaultNumber(), CreatedBy, Now);

        act.Should().Throw<OwnerRequiredException>();
    }

    [Fact(DisplayName = "Create: INV-4 — canal Parceiro sem partner_id lança PartnerRequiredException")]
    public void Create_PartnerChannelWithoutPartnerId_ThrowsPartnerRequiredException()
    {
        var act = () => Opportunity.Create(
            TenantId, BuId, AccountId, OwnerId, null, // partner_id = null
            DefaultStage(), PartnerChannel(), "Teste",
            DefaultContractValue(), DefaultProbability(), null, null,
            DefaultNumber(), CreatedBy, Now);

        act.Should().Throw<PartnerRequiredException>();
    }

    [Fact(DisplayName = "Create: canal Parceiro com partner_id válido passa (INV-4)")]
    public void Create_PartnerChannelWithPartnerId_Succeeds()
    {
        var partnerId = Guid.NewGuid();
        var act = () => Opportunity.Create(
            TenantId, BuId, AccountId, OwnerId, partnerId,
            DefaultStage(), PartnerChannel(), "Teste",
            DefaultContractValue(), DefaultProbability(), null, null,
            DefaultNumber(), CreatedBy, Now);

        act.Should().NotThrow();
    }

    [Fact(DisplayName = "Create: INV-5 — opportunity_number preservado após criação")]
    public void Create_OpportunityNumber_IsPreserved()
    {
        var number = new OpportunityNumber("AZ-0042");
        var opp = Opportunity.Create(
            TenantId, BuId, AccountId, OwnerId, null,
            DefaultStage(), DirectChannel(), "Teste",
            DefaultContractValue(), DefaultProbability(), null, null,
            number, CreatedBy, Now);

        opp.Number.Value.Should().Be("AZ-0042");
    }

    [Fact(DisplayName = "Create: estágio nulo lança ArgumentNullException (INV-2)")]
    public void Create_NullStage_ThrowsArgumentNullException()
    {
        var act = () => Opportunity.Create(
            TenantId, BuId, AccountId, OwnerId, null,
            null!, DirectChannel(), "Teste",
            DefaultContractValue(), DefaultProbability(), null, null,
            DefaultNumber(), CreatedBy, Now);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Create: canal nulo lança ArgumentNullException (INV-3)")]
    public void Create_NullOriginChannel_ThrowsArgumentNullException()
    {
        var act = () => Opportunity.Create(
            TenantId, BuId, AccountId, OwnerId, null,
            DefaultStage(), null!, "Teste",
            DefaultContractValue(), DefaultProbability(), null, null,
            DefaultNumber(), CreatedBy, Now);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "ADR-0008: Create — Currency da oportunidade reflete ContractValue")]
    public void Create_Currency_ReflectsContractValue()
    {
        var cv = new ContractValue(new Money(10000L, "USD"), Money.Zero("USD"), 0);
        var opp = Opportunity.Create(
            TenantId, BuId, AccountId, OwnerId, null,
            DefaultStage(), DirectChannel(), "Teste USD",
            cv, DefaultProbability(), null, null,
            DefaultNumber(), CreatedBy, Now);

        opp.Currency.Should().Be("USD");
    }
}

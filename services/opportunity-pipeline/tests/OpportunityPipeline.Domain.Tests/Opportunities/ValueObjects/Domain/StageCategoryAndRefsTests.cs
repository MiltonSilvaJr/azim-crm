using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Tests.Opportunities.ValueObjects.Domain;

/// <summary>
/// Testes para StageCategory, StageRef, OriginChannelRef, LossReasonRef, ContactLink.
/// Mapeia: Req 4, Req 5, Req 6, Req 16, INV-2, INV-11, TASK-03.
/// </summary>
public sealed class StageCategoryAndRefsTests
{
    [Fact(DisplayName = "StageCategory: possui exatamente 3 valores (open, won, lost)")]
    public void StageCategory_HasThreeValues()
    {
        var values = Enum.GetValues<StageCategory>().ToList();
        values.Should().HaveCount(3);
        values.Should().Contain(StageCategory.Open);
        values.Should().Contain(StageCategory.Won);
        values.Should().Contain(StageCategory.Lost);
    }

    [Fact(DisplayName = "StageRef: igualdade por valor")]
    public void StageRef_EqualityByValue()
    {
        var stageId = Guid.NewGuid();
        var a = new StageRef(stageId, "Qualificação", StageCategory.Open, 20, 1);
        var b = new StageRef(stageId, "Qualificação", StageCategory.Open, 20, 1);
        a.Should().Be(b);
    }

    [Fact(DisplayName = "StageRef: imutável (record)")]
    public void StageRef_IsImmutable()
    {
        var stageId = Guid.NewGuid();
        var stageRef = new StageRef(stageId, "Proposta Enviada", StageCategory.Open, 60, 3);
        stageRef.StageId.Should().Be(stageId);
        stageRef.Name.Should().Be("Proposta Enviada");
        stageRef.Category.Should().Be(StageCategory.Open);
        stageRef.DefaultProbability.Should().Be(60);
        stageRef.Order.Should().Be(3);
    }

    [Fact(DisplayName = "OriginChannelRef: is_partner_channel true para canal Parceiro")]
    public void OriginChannelRef_IsPartnerChannel()
    {
        var channelId = Guid.NewGuid();
        var channelRef = new OriginChannelRef(channelId, "Parceiro", IsPartnerChannel: true);
        channelRef.IsPartnerChannel.Should().BeTrue();
    }

    [Fact(DisplayName = "OriginChannelRef: is_partner_channel false para canal direto")]
    public void OriginChannelRef_IsNotPartnerChannel()
    {
        var channelId = Guid.NewGuid();
        var channelRef = new OriginChannelRef(channelId, "Direto", IsPartnerChannel: false);
        channelRef.IsPartnerChannel.Should().BeFalse();
    }

    [Fact(DisplayName = "LossReasonRef: igualdade por valor")]
    public void LossReasonRef_EqualityByValue()
    {
        var id = Guid.NewGuid();
        var a = new LossReasonRef(id, "Preço");
        var b = new LossReasonRef(id, "Preço");
        a.Should().Be(b);
    }

    [Fact(DisplayName = "ContactLink: is_primary=true")]
    public void ContactLink_IsPrimary()
    {
        var contactId = Guid.NewGuid();
        var link = new ContactLink(contactId, IsPrimary: true);
        link.ContactId.Should().Be(contactId);
        link.IsPrimary.Should().BeTrue();
    }

    [Fact(DisplayName = "ContactLink: is_primary=false")]
    public void ContactLink_IsNotPrimary()
    {
        var contactId = Guid.NewGuid();
        var link = new ContactLink(contactId, IsPrimary: false);
        link.IsPrimary.Should().BeFalse();
    }

    [Fact(DisplayName = "CommissionCalculation: imutável com todos os campos")]
    public void CommissionCalculation_IsImmutable()
    {
        var calc = new CommissionCalculation(
            ComissaoSetup: new Money(10000L),
            ComissaoRecorrente: new Money(5000L),
            ComissaoTotal: new Money(15000L));

        calc.ComissaoSetup.AmountInCents.Should().Be(10000L);
        calc.ComissaoRecorrente.AmountInCents.Should().Be(5000L);
        calc.ComissaoTotal.AmountInCents.Should().Be(15000L);
    }

    [Fact(DisplayName = "CommissionDefaults: contém percentuais de setup e recorrente")]
    public void CommissionDefaults_HasPercentages()
    {
        var defaults = new CommissionDefaults(PctSetup: 10m, PctRecorrente: 5m);
        defaults.PctSetup.Should().Be(10m);
        defaults.PctRecorrente.Should().Be(5m);
    }

    [Fact(DisplayName = "CommissionRole: possui 4 valores (Indicador, Revendedor, Distribuidor, Integrador)")]
    public void CommissionRole_HasFourValues()
    {
        var values = Enum.GetValues<CommissionRole>().ToList();
        values.Should().HaveCount(4);
        values.Should().Contain(CommissionRole.Indicador);
        values.Should().Contain(CommissionRole.Revendedor);
        values.Should().Contain(CommissionRole.Distribuidor);
        values.Should().Contain(CommissionRole.Integrador);
    }
}

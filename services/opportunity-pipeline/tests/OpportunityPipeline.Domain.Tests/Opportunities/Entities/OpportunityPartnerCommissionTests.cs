using OpportunityPipeline.Domain.Opportunities.Entities;
using OpportunityPipeline.Domain.Opportunities.Exceptions;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Tests.Opportunities.Entities;

/// <summary>
/// Testes para OpportunityPartnerCommission — freeze/snapshot imutável + herança de moeda (ADR-0008).
/// Mapeia: Req 11, Req 14, RNF 5, INV-12, DD-002, ADR-0008, TASK-05.
/// </summary>
public sealed class OpportunityPartnerCommissionTests
{
    private static OpportunityPartnerCommission CreateProjected(string currency = "BRL")
    {
        var terms = new CommissionTerms(CommissionRole.Revendedor, 10m, 5m, null, 12);
        var calculation = new CommissionCalculation(
            new Money(1000L, currency), new Money(3000L, currency), new Money(4000L, currency));

        return new OpportunityPartnerCommission(
            id: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            opportunityId: Guid.NewGuid(),
            partnerId: Guid.NewGuid(),
            terms: terms,
            calculation: calculation,
            currency: currency);
    }

    [Fact(DisplayName = "OpportunityPartnerCommission: criado como projetado (is_snapshot = false)")]
    public void Commission_CreatedAsProjected()
    {
        var commission = CreateProjected();
        commission.IsSnapshot.Should().BeFalse();
        commission.SnapshotAt.Should().BeNull();
    }

    [Fact(DisplayName = "OpportunityPartnerCommission: Freeze() retorna novo registro com is_snapshot = true")]
    public void Commission_Freeze_ReturnsSnapshotWithIsSnapshotTrue()
    {
        var commission = CreateProjected();
        var snapshotAt = DateTimeOffset.UtcNow;
        var snapshot = commission.Freeze(snapshotAt);

        snapshot.IsSnapshot.Should().BeTrue();
        snapshot.SnapshotAt.Should().Be(snapshotAt);
    }

    [Fact(DisplayName = "OpportunityPartnerCommission: Freeze() — termos congelados iguais aos da projetada")]
    public void Commission_Freeze_FrozenTermsEqualProjectedTerms()
    {
        var commission = CreateProjected();
        var snapshotAt = DateTimeOffset.UtcNow;
        var snapshot = commission.Freeze(snapshotAt);

        snapshot.Terms.Should().Be(commission.Terms);
        snapshot.Calculation.Should().Be(commission.Calculation);
    }

    [Fact(DisplayName = "OpportunityPartnerCommission: Freeze() — IDs copiados corretamente")]
    public void Commission_Freeze_IdsCopiedCorrectly()
    {
        var commission = CreateProjected();
        var snapshot = commission.Freeze(DateTimeOffset.UtcNow);

        snapshot.TenantId.Should().Be(commission.TenantId);
        snapshot.OpportunityId.Should().Be(commission.OpportunityId);
        snapshot.PartnerId.Should().Be(commission.PartnerId);
        snapshot.Id.Should().NotBe(commission.Id, because: "snapshot tem novo ID");
    }

    [Fact(DisplayName = "OpportunityPartnerCommission: Freeze() sobre snapshot lança SnapshotImmutableException")]
    public void Commission_FreezeOnSnapshot_ThrowsSnapshotImmutableException()
    {
        var commission = CreateProjected();
        var snapshot = commission.Freeze(DateTimeOffset.UtcNow);

        var act = () => snapshot.Freeze(DateTimeOffset.UtcNow);
        act.Should().Throw<SnapshotImmutableException>();
    }

    [Fact(DisplayName = "OpportunityPartnerCommission: snapshot é imutável — não aceita WithTerms")]
    public void Commission_Snapshot_IsImmutable_CannotUpdateTerms()
    {
        var commission = CreateProjected();
        var snapshot = commission.Freeze(DateTimeOffset.UtcNow);
        var newTerms = new CommissionTerms(CommissionRole.Indicador, 20m, 0m, null, 0);
        var newCalc = new CommissionCalculation(new Money(2000L, "BRL"), Money.Zero("BRL"), new Money(2000L, "BRL"));

        var act = () => snapshot.WithUpdatedCalculation(newTerms, newCalc);
        act.Should().Throw<SnapshotImmutableException>();
    }

    [Fact(DisplayName = "OpportunityPartnerCommission: projetada aceita WithUpdatedCalculation")]
    public void Commission_Projected_AcceptsWithUpdatedCalculation()
    {
        var commission = CreateProjected();
        var newTerms = new CommissionTerms(CommissionRole.Indicador, 20m, 0m, null, 0);
        var newCalc = new CommissionCalculation(new Money(2000L, "BRL"), Money.Zero("BRL"), new Money(2000L, "BRL"));

        var updated = commission.WithUpdatedCalculation(newTerms, newCalc);
        updated.Terms.Should().Be(newTerms);
        updated.Calculation.Should().Be(newCalc);
        updated.IsSnapshot.Should().BeFalse();
    }

    // =========================================================================
    // ADR-0008 — herança de moeda da oportunidade
    // =========================================================================

    [Fact(DisplayName = "ADR-0008: comissão herda currency da oportunidade (USD)")]
    public void Commission_InheritsCurrency_USD()
    {
        var commission = CreateProjected("USD");
        commission.Currency.Should().Be("USD");
        commission.Calculation.ComissaoSetup.Currency.Should().Be("USD");
        commission.Calculation.ComissaoTotal.Currency.Should().Be("USD");
    }

    [Fact(DisplayName = "ADR-0008: comissão herda currency da oportunidade (EUR)")]
    public void Commission_InheritsCurrency_EUR()
    {
        var commission = CreateProjected("EUR");
        commission.Currency.Should().Be("EUR");
        commission.Calculation.ComissaoRecorrente.Currency.Should().Be("EUR");
    }

    [Fact(DisplayName = "ADR-0008: Freeze() preserva currency do snapshot")]
    public void Commission_Freeze_PreservesCurrency()
    {
        var commission = CreateProjected("USD");
        var snapshot = commission.Freeze(DateTimeOffset.UtcNow);

        snapshot.Currency.Should().Be("USD");
    }

    [Theory(DisplayName = "ADR-0008: comissão aceita todas as moedas suportadas")]
    [InlineData("BRL")]
    [InlineData("USD")]
    [InlineData("EUR")]
    public void Commission_AcceptsAllSupportedCurrencies(string currency)
    {
        var act = () => CreateProjected(currency);
        act.Should().NotThrow();
    }
}

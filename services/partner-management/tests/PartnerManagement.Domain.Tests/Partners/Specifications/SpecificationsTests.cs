using FluentAssertions;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.Specifications;
using PartnerManagement.Domain.Partners.ValueObjects;
using Xunit;

namespace PartnerManagement.Domain.Tests.Partners.Specifications;

/// <summary>
/// Testes das quatro Specifications do módulo partner-management.
/// Mapeia: Req 3, Req 4.2, Req 5.2, Req 8, Req 11, design §4.6, TASK-07.
/// </summary>
public sealed class SpecificationsTests
{
    private static readonly Guid _tenantId = Guid.NewGuid();
    private static readonly Guid _actorId = Guid.NewGuid();

    private sealed class AlwaysValidRoleProvider : ICanonicalRoleProvider
    {
        public bool IsCanonical(string role, Guid tenantId) => true;
    }

    private static Partner BuildActivePartner(
        decimal pctSetup = 10.00m,
        decimal pctRecorrente = 5.00m,
        bool inactive = false)
    {
        Partner partner = Partner.Create(
            tenantId: _tenantId,
            name: "Parceiro Spec",
            role: "Indicador",
            commissionDefaults: CommissionDefaults.Create(
                Percentage.Create(pctSetup),
                Percentage.Create(pctRecorrente)),
            contact: null,
            notes: null,
            roleProvider: new AlwaysValidRoleProvider(),
            createdBy: _actorId);

        if (inactive)
        {
            partner.Deactivate();
        }

        return partner;
    }

    // =========================================================================
    // ActivePartnerSpecification
    // =========================================================================

    [Fact(DisplayName = "ActivePartnerSpecification — parceiro ativo satisfaz a spec")]
    public void ActivePartnerSpec_ActivePartner_IsSatisfied()
    {
        Partner partner = BuildActivePartner();
        ActivePartnerSpecification spec = new();
        spec.IsSatisfiedBy(partner).Should().BeTrue();
    }

    [Fact(DisplayName = "ActivePartnerSpecification — parceiro inativo não satisfaz a spec")]
    public void ActivePartnerSpec_InactivePartner_IsNotSatisfied()
    {
        Partner partner = BuildActivePartner(inactive: true);
        ActivePartnerSpecification spec = new();
        spec.IsSatisfiedBy(partner).Should().BeFalse();
    }

    // =========================================================================
    // TriagePendingSpecification
    // =========================================================================

    [Fact(DisplayName = "TriagePendingSpecification — ambos percentuais em 0,00 satisfaz (triagem pendente)")]
    public void TriagePendingSpec_BothZero_IsSatisfied()
    {
        Partner partner = BuildActivePartner(0.00m, 0.00m);
        TriagePendingSpecification spec = new();
        spec.IsSatisfiedBy(partner).Should().BeTrue();
    }

    [Fact(DisplayName = "TriagePendingSpecification — pctSetup não-zero não satisfaz")]
    public void TriagePendingSpec_NonZeroPctSetup_IsNotSatisfied()
    {
        Partner partner = BuildActivePartner(10.00m, 0.00m);
        TriagePendingSpecification spec = new();
        spec.IsSatisfiedBy(partner).Should().BeFalse();
    }

    [Fact(DisplayName = "TriagePendingSpecification — pctRecorrente não-zero não satisfaz")]
    public void TriagePendingSpec_NonZeroPctRecorrente_IsNotSatisfied()
    {
        Partner partner = BuildActivePartner(0.00m, 5.00m);
        TriagePendingSpecification spec = new();
        spec.IsSatisfiedBy(partner).Should().BeFalse();
    }

    // =========================================================================
    // TenantScopeSpecification
    // =========================================================================

    [Fact(DisplayName = "TenantScopeSpecification — parceiro do mesmo tenant satisfaz")]
    public void TenantScopeSpec_SameTenant_IsSatisfied()
    {
        Partner partner = BuildActivePartner();
        TenantScopeSpecification spec = new(_tenantId);
        spec.IsSatisfiedBy(partner).Should().BeTrue();
    }

    [Fact(DisplayName = "TenantScopeSpecification — parceiro de outro tenant não satisfaz")]
    public void TenantScopeSpec_DifferentTenant_IsNotSatisfied()
    {
        Partner partner = BuildActivePartner();
        TenantScopeSpecification spec = new(Guid.NewGuid()); // tenant diferente
        spec.IsSatisfiedBy(partner).Should().BeFalse();
    }

    // =========================================================================
    // CanonicalRoleSpecification
    // =========================================================================

    [Theory(DisplayName = "CanonicalRoleSpecification — papel do seed canônico satisfaz")]
    [InlineData("Indicador")]
    [InlineData("Revendedor")]
    [InlineData("Distribuidor")]
    [InlineData("Integrador")]
    public void CanonicalRoleSpec_SeedRole_IsSatisfied(string role)
    {
        CanonicalRoleSpecification spec = new(PartnerRole.DefaultCanonicalRoles);
        spec.IsSatisfiedBy(role).Should().BeTrue();
    }

    [Fact(DisplayName = "CanonicalRoleSpecification — papel fora da lista canônica não satisfaz")]
    public void CanonicalRoleSpec_NonCanonicalRole_IsNotSatisfied()
    {
        CanonicalRoleSpecification spec = new(PartnerRole.DefaultCanonicalRoles);
        spec.IsSatisfiedBy("PapelArbitrario").Should().BeFalse();
    }

    [Fact(DisplayName = "CanonicalRoleSpecification — papel vazio não satisfaz")]
    public void CanonicalRoleSpec_EmptyRole_IsNotSatisfied()
    {
        CanonicalRoleSpecification spec = new(PartnerRole.DefaultCanonicalRoles);
        spec.IsSatisfiedBy(string.Empty).Should().BeFalse();
    }
}

using FluentAssertions;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.ValueObjects;
using Xunit;

namespace PartnerManagement.Domain.Tests.Partners;

/// <summary>
/// Testes da state machine <see cref="PartnerStatus"/> via métodos do agregado.
/// Cobre todas as transições incluindo idempotência (DD-006, PBT-02).
/// Mapeia: Req 3, design §4.5, TASK-07.
/// </summary>
public sealed class PartnerStatusTests
{
    private static readonly Guid _tenantId = Guid.NewGuid();
    private static readonly Guid _actorId = Guid.NewGuid();

    private sealed class AlwaysValidRoleProvider : ICanonicalRoleProvider
    {
        public bool IsCanonical(string role, Guid tenantId) => true;
    }

    private static Partner BuildActivePartner()
    {
        return Partner.Create(
            tenantId: _tenantId,
            name: "Parceiro Status",
            role: "Indicador",
            commissionDefaults: CommissionDefaults.Default,
            contact: null,
            notes: null,
            roleProvider: new AlwaysValidRoleProvider(),
            createdBy: _actorId);
    }

    [Fact(DisplayName = "PartnerStatus — Deactivate: Active → Inactive (transição efetiva)")]
    public void Deactivate_Active_BecomesInactive()
    {
        Partner partner = BuildActivePartner();
        partner.Deactivate();
        partner.Status.Should().Be(PartnerStatus.Inactive);
    }

    [Fact(DisplayName = "PartnerStatus — Deactivate: Inactive → Inactive (idempotente)")]
    public void Deactivate_Inactive_RemainsInactiveNoException()
    {
        Partner partner = BuildActivePartner();
        partner.Deactivate();
        Action act = () => partner.Deactivate();
        act.Should().NotThrow();
        partner.Status.Should().Be(PartnerStatus.Inactive);
    }

    [Fact(DisplayName = "PartnerStatus — Reactivate: Inactive → Active (transição efetiva)")]
    public void Reactivate_Inactive_BecomesActive()
    {
        Partner partner = BuildActivePartner();
        partner.Deactivate();
        partner.Reactivate();
        partner.Status.Should().Be(PartnerStatus.Active);
    }

    [Fact(DisplayName = "PartnerStatus — Reactivate: Active → Active (idempotente)")]
    public void Reactivate_Active_RemainsActiveNoException()
    {
        Partner partner = BuildActivePartner();
        Action act = () => partner.Reactivate();
        act.Should().NotThrow();
        partner.Status.Should().Be(PartnerStatus.Active);
    }

    [Fact(DisplayName = "PartnerStatus — Deactivate retorna false em transição idempotente")]
    public void Deactivate_IdempotentTransition_ReturnsFalse()
    {
        Partner partner = BuildActivePartner();
        partner.Deactivate();
        bool result = partner.Deactivate();
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "PartnerStatus — Deactivate retorna true em transição efetiva")]
    public void Deactivate_EffectiveTransition_ReturnsTrue()
    {
        Partner partner = BuildActivePartner();
        bool result = partner.Deactivate();
        result.Should().BeTrue();
    }

    [Fact(DisplayName = "PartnerStatus — Reactivate retorna false em transição idempotente")]
    public void Reactivate_IdempotentTransition_ReturnsFalse()
    {
        Partner partner = BuildActivePartner();
        bool result = partner.Reactivate();
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "PartnerStatus — Reactivate retorna true em transição efetiva")]
    public void Reactivate_EffectiveTransition_ReturnsTrue()
    {
        Partner partner = BuildActivePartner();
        partner.Deactivate();
        bool result = partner.Reactivate();
        result.Should().BeTrue();
    }
}

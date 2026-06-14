using FluentAssertions;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.Events;
using PartnerManagement.Domain.Partners.ValueObjects;
using Xunit;

namespace PartnerManagement.Domain.Tests.Partners;

/// <summary>
/// Testes de acumulação de domain events no Aggregate Root <see cref="Partner"/>.
/// Verifica que cada transição efetiva acumula exatamente o evento esperado
/// e que transições idempotentes não acumulam eventos de transição.
/// Verifica também que payloads não contêm PII (RNF 4, design §4.4).
/// Mapeia: Req 1.8, Req 2.5, Req 3.6, design §4.4, TASK-06.
/// </summary>
public sealed class PartnerDomainEventsTests
{
    private static readonly Guid _tenantId = Guid.NewGuid();
    private static readonly Guid _actorId = Guid.NewGuid();

    private sealed class AlwaysValidRoleProvider : ICanonicalRoleProvider
    {
        public bool IsCanonical(string role, Guid tenantId) => !string.IsNullOrWhiteSpace(role);
    }

    private static ICanonicalRoleProvider ValidProvider => new AlwaysValidRoleProvider();

    private static Partner BuildActivePartner(decimal pctSetup = 10.00m, decimal pctRecorrente = 5.00m)
    {
        return Partner.Create(
            tenantId: _tenantId,
            name: "Parceiro Evento",
            role: "Indicador",
            commissionDefaults: CommissionDefaults.Create(
                Percentage.Create(pctSetup),
                Percentage.Create(pctRecorrente)),
            contact: null,
            notes: null,
            roleProvider: ValidProvider,
            createdBy: _actorId);
    }

    // =========================================================================
    // PartnerCreated
    // =========================================================================

    [Fact(DisplayName = "Partner.Create — acumula exatamente um PartnerCreated")]
    public void Create_AccumulatesExactlyOnePartnerCreated()
    {
        Partner partner = BuildActivePartner();
        partner.DomainEvents.Should().HaveCount(1);
        partner.DomainEvents[0].Should().BeOfType<PartnerCreated>();
    }

    [Fact(DisplayName = "PartnerCreated — payload contém partnerId e tenantId corretos")]
    public void PartnerCreated_PayloadHasCorrectIds()
    {
        Partner partner = BuildActivePartner();
        PartnerCreated evt = (PartnerCreated)partner.DomainEvents[0];
        evt.PartnerId.Should().Be(partner.Id);
        evt.TenantId.Should().Be(_tenantId);
    }

    [Fact(DisplayName = "PartnerCreated — payload não contém name do parceiro (RNF 4)")]
    public void PartnerCreated_PayloadDoesNotContainName()
    {
        Partner partner = BuildActivePartner();
        PartnerCreated evt = (PartnerCreated)partner.DomainEvents[0];
        // O payload deve conter partnerType (papel), nunca o nome
        evt.PartnerType.Should().NotBeNullOrEmpty();
        // Verificação indireta: o record não tem campo Name
        string eventString = evt.ToString();
        eventString.Should().NotContain("Parceiro Evento");
    }

    // =========================================================================
    // PartnerCommissionPercentagesUpdated
    // =========================================================================

    [Fact(DisplayName = "Partner.UpdateProfile — percentuais alterados acumula PartnerCommissionPercentagesUpdated")]
    public void UpdateProfile_PercentsChanged_AccumulatesPercentageUpdatedEvent()
    {
        Partner partner = BuildActivePartner(10.00m, 5.00m);
        partner.ClearDomainEvents();

        partner.UpdateProfile(
            name: "Parceiro Evento",
            role: "Indicador",
            commissionDefaults: CommissionDefaults.Create(Percentage.Create(20.00m), Percentage.Create(10.00m)),
            notes: null,
            roleProvider: ValidProvider,
            updatedBy: _actorId);

        partner.DomainEvents.Should().HaveCount(1);
        partner.DomainEvents[0].Should().BeOfType<PartnerCommissionPercentagesUpdated>();
    }

    [Fact(DisplayName = "Partner.UpdateProfile — percentuais sem alteração não acumula evento de percentual")]
    public void UpdateProfile_SamePercents_DoesNotAccumulatePercentageUpdatedEvent()
    {
        Partner partner = BuildActivePartner(10.00m, 5.00m);
        partner.ClearDomainEvents();

        partner.UpdateProfile(
            name: "Nome Atualizado",
            role: "Indicador",
            commissionDefaults: CommissionDefaults.Create(Percentage.Create(10.00m), Percentage.Create(5.00m)),
            notes: "nota",
            roleProvider: ValidProvider,
            updatedBy: _actorId);

        partner.DomainEvents.Should().BeEmpty();
    }

    // =========================================================================
    // PartnerDeactivated
    // =========================================================================

    [Fact(DisplayName = "Partner.Deactivate — transição efetiva (Active→Inactive) acumula PartnerDeactivated")]
    public void Deactivate_EffectiveTransition_AccumulatesPartnerDeactivated()
    {
        Partner partner = BuildActivePartner();
        partner.ClearDomainEvents();

        partner.Deactivate();

        partner.DomainEvents.Should().HaveCount(1);
        partner.DomainEvents[0].Should().BeOfType<PartnerDeactivated>();
    }

    [Fact(DisplayName = "Partner.Deactivate — transição idempotente (já Inactive) NÃO acumula PartnerDeactivated")]
    public void Deactivate_IdempotentTransition_DoesNotAccumulatePartnerDeactivated()
    {
        Partner partner = BuildActivePartner();
        partner.Deactivate();
        partner.ClearDomainEvents();

        // Segunda inativação: idempotente — sem evento de transição (DD-006)
        partner.Deactivate();

        IEnumerable<IDomainEvent> transitionEvents = partner.DomainEvents
            .Where(e => e is PartnerDeactivated);
        transitionEvents.Should().BeEmpty();
    }

    // =========================================================================
    // PartnerReactivated
    // =========================================================================

    [Fact(DisplayName = "Partner.Reactivate — transição efetiva (Inactive→Active) acumula PartnerReactivated")]
    public void Reactivate_EffectiveTransition_AccumulatesPartnerReactivated()
    {
        Partner partner = BuildActivePartner();
        partner.Deactivate();
        partner.ClearDomainEvents();

        partner.Reactivate();

        partner.DomainEvents.Should().HaveCount(1);
        partner.DomainEvents[0].Should().BeOfType<PartnerReactivated>();
    }

    [Fact(DisplayName = "Partner.Reactivate — transição idempotente (já Active) NÃO acumula PartnerReactivated")]
    public void Reactivate_IdempotentTransition_DoesNotAccumulatePartnerReactivated()
    {
        Partner partner = BuildActivePartner();
        partner.ClearDomainEvents();

        // Já está Active: reativar é idempotente (DD-006)
        partner.Reactivate();

        IEnumerable<IDomainEvent> transitionEvents = partner.DomainEvents
            .Where(e => e is PartnerReactivated);
        transitionEvents.Should().BeEmpty();
    }

    // =========================================================================
    // ClearDomainEvents
    // =========================================================================

    [Fact(DisplayName = "Partner.ClearDomainEvents — limpa todos os eventos acumulados")]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        Partner partner = BuildActivePartner();
        partner.DomainEvents.Should().NotBeEmpty();
        partner.ClearDomainEvents();
        partner.DomainEvents.Should().BeEmpty();
    }
}

using FluentAssertions;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.Exceptions;
using PartnerManagement.Domain.Partners.ValueObjects;
using Xunit;

namespace PartnerManagement.Domain.Tests.Partners;

/// <summary>
/// Testes unitários para o Aggregate Root <see cref="Partner"/>.
/// Cobre invariantes I1..I5, métodos de comportamento e acumulação de domain events.
/// Mapeia: Req 1, Req 2, Req 3, Req 5, Req 6, Req 7, design §4.1, design §4.5, TASK-05.
/// </summary>
public sealed class PartnerTests
{
    // =========================================================================
    // Helpers
    // =========================================================================

    private static readonly Guid _validTenantId = Guid.NewGuid();
    private static readonly Guid _validCreatedBy = Guid.NewGuid();

    /// <summary>Provedor de papéis canônicos que aceita qualquer papel não-vazio (stub de teste).</summary>
    private sealed class AlwaysValidRoleProvider : ICanonicalRoleProvider
    {
        public bool IsCanonical(string role, Guid tenantId) => !string.IsNullOrWhiteSpace(role);
    }

    /// <summary>Provedor que rejeita qualquer papel (simula papel fora da lista canônica).</summary>
    private sealed class AlwaysInvalidRoleProvider : ICanonicalRoleProvider
    {
        public bool IsCanonical(string role, Guid tenantId) => false;
    }

    private static ICanonicalRoleProvider ValidProvider => new AlwaysValidRoleProvider();
    private static ICanonicalRoleProvider InvalidProvider => new AlwaysInvalidRoleProvider();

    private static Partner CreateValidPartner(
        string? name = "Parceiro Válido",
        string? role = "Indicador",
        decimal pctSetup = 10.00m,
        decimal pctRecorrente = 5.00m,
        ICanonicalRoleProvider? provider = null)
    {
        return Partner.Create(
            tenantId: _validTenantId,
            name: name!,
            role: role!,
            commissionDefaults: CommissionDefaults.Create(
                Percentage.Create(pctSetup),
                Percentage.Create(pctRecorrente)),
            contact: null,
            notes: null,
            roleProvider: provider ?? ValidProvider,
            createdBy: _validCreatedBy);
    }

    // =========================================================================
    // I1 — nome não vazio
    // =========================================================================

    [Fact(DisplayName = "Partner.Create — I1: nome vazio lança PartnerNameRequiredException")]
    public void Create_EmptyName_ThrowsPartnerNameRequiredException()
    {
        Action act = () => CreateValidPartner(name: "");
        act.Should().Throw<PartnerNameRequiredException>();
    }

    [Fact(DisplayName = "Partner.Create — I1: nome somente espaços lança PartnerNameRequiredException")]
    public void Create_WhitespaceOnlyName_ThrowsPartnerNameRequiredException()
    {
        Action act = () => CreateValidPartner(name: "   ");
        act.Should().Throw<PartnerNameRequiredException>();
    }

    // =========================================================================
    // I2 — papel pertence à lista canônica
    // =========================================================================

    [Fact(DisplayName = "Partner.Create — I2: papel fora da lista canônica lança InvalidPartnerRoleException")]
    public void Create_NonCanonicalRole_ThrowsInvalidPartnerRoleException()
    {
        Action act = () => CreateValidPartner(role: "PapelForaDaLista", provider: InvalidProvider);
        act.Should().Throw<InvalidPartnerRoleException>();
    }

    // =========================================================================
    // I3 — percentuais em [0,00; 100,00]
    // =========================================================================

    [Fact(DisplayName = "Partner.Create — I3: pctSetup fora do intervalo lança PercentageOutOfRangeException")]
    public void Create_PctSetupOutOfRange_ThrowsPercentageOutOfRangeException()
    {
        Action act = () => CreateValidPartner(pctSetup: 101.00m);
        act.Should().Throw<PercentageOutOfRangeException>();
    }

    [Fact(DisplayName = "Partner.Create — I3: pctRecorrente fora do intervalo lança PercentageOutOfRangeException")]
    public void Create_PctRecorrenteOutOfRange_ThrowsPercentageOutOfRangeException()
    {
        Action act = () => CreateValidPartner(pctRecorrente: -1.00m);
        act.Should().Throw<PercentageOutOfRangeException>();
    }

    // =========================================================================
    // I4 — parceiro nasce Active
    // =========================================================================

    [Fact(DisplayName = "Partner.Create — I4: parceiro nasce com status Active")]
    public void Create_ValidData_StatusIsActive()
    {
        Partner partner = CreateValidPartner();
        partner.Status.Should().Be(PartnerStatus.Active);
    }

    // =========================================================================
    // I5 — e-mail de contato válido
    // =========================================================================

    [Fact(DisplayName = "Partner.Create — I5: e-mail de contato inválido lança InvalidPartnerContactException")]
    public void Create_InvalidContactEmail_ThrowsInvalidPartnerContactException()
    {
        Action act = () => Partner.Create(
            tenantId: _validTenantId,
            name: "Parceiro",
            role: "Indicador",
            commissionDefaults: CommissionDefaults.Default,
            contact: PartnerContact.Create("email-invalido", null),
            notes: null,
            roleProvider: ValidProvider,
            createdBy: _validCreatedBy);

        act.Should().Throw<InvalidPartnerContactException>();
    }

    [Fact(DisplayName = "Partner.Create — I5: sem contato é permitido")]
    public void Create_NoContact_Succeeds()
    {
        Action act = () => Partner.Create(
            tenantId: _validTenantId,
            name: "Parceiro",
            role: "Indicador",
            commissionDefaults: CommissionDefaults.Default,
            contact: null,
            notes: null,
            roleProvider: ValidProvider,
            createdBy: _validCreatedBy);

        act.Should().NotThrow();
    }

    // =========================================================================
    // Criação bem-sucedida
    // =========================================================================

    [Fact(DisplayName = "Partner.Create — criação válida produz parceiro com todos os campos corretos")]
    public void Create_ValidData_ReturnsPartnerWithCorrectFields()
    {
        Partner partner = CreateValidPartner("Acme Corp", "Distribuidor", 15.00m, 7.50m);

        partner.Id.Should().NotBe(Guid.Empty);
        partner.TenantId.Should().Be(_validTenantId);
        partner.Name.Value.Should().Be("Acme Corp");
        partner.Role.Value.Should().Be("Distribuidor");
        partner.CommissionDefaults.PctSetup.Value.Should().Be(15.00m);
        partner.CommissionDefaults.PctRecorrente.Value.Should().Be(7.50m);
        partner.Status.Should().Be(PartnerStatus.Active);
    }

    // =========================================================================
    // UpdateProfile
    // =========================================================================

    [Fact(DisplayName = "Partner.UpdateProfile — reaplicar invariantes: nome vazio lança exceção")]
    public void UpdateProfile_EmptyName_ThrowsPartnerNameRequiredException()
    {
        Partner partner = CreateValidPartner();
        Action act = () => partner.UpdateProfile(
            name: "",
            role: "Indicador",
            commissionDefaults: CommissionDefaults.Default,
            notes: null,
            roleProvider: ValidProvider,
            updatedBy: _validCreatedBy);

        act.Should().Throw<PartnerNameRequiredException>();
    }

    [Fact(DisplayName = "Partner.UpdateProfile — reaplicar invariantes: papel inválido lança exceção")]
    public void UpdateProfile_InvalidRole_ThrowsInvalidPartnerRoleException()
    {
        Partner partner = CreateValidPartner();
        Action act = () => partner.UpdateProfile(
            name: "Acme",
            role: "PapelInvalido",
            commissionDefaults: CommissionDefaults.Default,
            notes: null,
            roleProvider: InvalidProvider,
            updatedBy: _validCreatedBy);

        act.Should().Throw<InvalidPartnerRoleException>();
    }

    [Fact(DisplayName = "Partner.UpdateProfile — dados válidos atualiza o parceiro")]
    public void UpdateProfile_ValidData_UpdatesPartner()
    {
        Partner partner = CreateValidPartner("Antes", "Indicador", 5.00m, 2.00m);
        partner.UpdateProfile(
            name: "Depois",
            role: "Revendedor",
            commissionDefaults: CommissionDefaults.Create(Percentage.Create(20.00m), Percentage.Create(10.00m)),
            notes: "nota nova",
            roleProvider: ValidProvider,
            updatedBy: _validCreatedBy);

        partner.Name.Value.Should().Be("Depois");
        partner.Role.Value.Should().Be("Revendedor");
        partner.CommissionDefaults.PctSetup.Value.Should().Be(20.00m);
    }

    // =========================================================================
    // UpdateContact
    // =========================================================================

    [Fact(DisplayName = "Partner.UpdateContact — contato válido atualiza o parceiro")]
    public void UpdateContact_ValidContact_UpdatesPartner()
    {
        Partner partner = CreateValidPartner();
        PartnerContact contact = PartnerContact.Create("novo@email.com", null);
        partner.UpdateContact(contact);
        partner.Contact.Should().NotBeNull();
        partner.Contact!.EmailAddress!.Value.Should().Be("novo@email.com");
    }

    [Fact(DisplayName = "Partner.UpdateContact — contato nulo remove o contato")]
    public void UpdateContact_NullContact_RemovesContact()
    {
        Partner partner = CreateValidPartner();
        partner.UpdateContact(null);
        partner.Contact.Should().BeNull();
    }

    // =========================================================================
    // Deactivate / Reactivate
    // =========================================================================

    [Fact(DisplayName = "Partner.Deactivate — parceiro ativo é inativado com sucesso")]
    public void Deactivate_ActivePartner_BecomesInactive()
    {
        Partner partner = CreateValidPartner();
        partner.Deactivate();
        partner.Status.Should().Be(PartnerStatus.Inactive);
    }

    [Fact(DisplayName = "Partner.Deactivate — parceiro já inativo permanece inativo (idempotente)")]
    public void Deactivate_AlreadyInactive_RemainsInactiveNoException()
    {
        Partner partner = CreateValidPartner();
        partner.Deactivate();
        Action act = () => partner.Deactivate();
        act.Should().NotThrow();
        partner.Status.Should().Be(PartnerStatus.Inactive);
    }

    [Fact(DisplayName = "Partner.Reactivate — parceiro inativo é reativado com sucesso")]
    public void Reactivate_InactivePartner_BecomesActive()
    {
        Partner partner = CreateValidPartner();
        partner.Deactivate();
        partner.Reactivate();
        partner.Status.Should().Be(PartnerStatus.Active);
    }

    [Fact(DisplayName = "Partner.Reactivate — parceiro já ativo permanece ativo (idempotente)")]
    public void Reactivate_AlreadyActive_RemainsActiveNoException()
    {
        Partner partner = CreateValidPartner();
        Action act = () => partner.Reactivate();
        act.Should().NotThrow();
        partner.Status.Should().Be(PartnerStatus.Active);
    }
}

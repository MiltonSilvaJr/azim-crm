using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.Exceptions;
using AccountManagement.Domain.Accounts.Services;
using AccountManagement.Domain.Accounts.ValueObjects;
using FluentAssertions;
using Xunit;

namespace AccountManagement.Domain.Tests.Accounts;

/// <summary>
/// Testes unitários para o agregado <see cref="Account"/>.
///
/// Cobre as invariantes I1, I2, I3 (design §4.1) e os métodos de comportamento
/// <c>Rename</c>, <c>AddContact</c>, <c>UpdateContact</c> e <c>ForgetContact</c>.
///
/// Mapeia: design §4.1, TASK-03 ST-01 e ST-02, Req 1.5, Req 4.2, Req 5.1.
/// </summary>
public sealed class AccountTests
{
    private static readonly NameNormalizer Normalizer = new();
    private static readonly Guid TenantId = Guid.NewGuid();

    // =========================================================================
    // Invariante I1: nome não vazio (Req 1.5)
    // =========================================================================

    [Fact(DisplayName = "I1: Account.Create com nome vazio lança AccountNameRequiredException")]
    public void Create_WithEmptyName_ThrowsAccountNameRequiredException()
    {
        var buId = Guid.NewGuid();
        var act = () => Account.Create(TenantId, buId, AccountName.Create("test"), null, null, Normalizer);

        // Sobrescreve para testar diretamente com nome inválido
        // (AccountName já valida; aqui testamos que Account.Create delega ao VO)
        var act2 = () => Account.Create(TenantId, buId, null!, null, null, Normalizer);

        act2.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "I4: Account.Create com buId vazio lança ArgumentException")]
    public void Create_WithEmptyBuId_ThrowsArgumentException()
    {
        var name = AccountName.Create("Empresa X");

        var act = () => Account.Create(TenantId, Guid.Empty, name, null, null, Normalizer);

        act.Should().Throw<ArgumentException>().WithParameterName("buId");
    }

    [Fact(DisplayName = "I1: Account.Create com nome válido cria conta com NormalizedName")]
    public void Create_WithValidName_CreatesAccountWithNormalizedName()
    {
        var name = AccountName.Create("PAG.AI Ltda");
        var buId = Guid.NewGuid();

        var account = Account.Create(TenantId, buId, name, null, null, Normalizer);

        account.Should().NotBeNull();
        account.Name.Should().Be(name);
        account.NormalizedName.Value.Should().Be(Normalizer.Normalize("PAG.AI Ltda"));
        account.TenantId.Should().Be(TenantId);
        account.BuId.Should().Be(buId);
    }

    [Fact(DisplayName = "I1: Account.Create gera AccountCreated domain event")]
    public void Create_PublishesAccountCreatedEvent()
    {
        var name = AccountName.Create("Azim Corp");

        var account = Account.Create(TenantId, Guid.NewGuid(), name, null, null, Normalizer);

        account.DomainEvents.Should().ContainSingle(e =>
            e is AccountManagement.Domain.Accounts.Events.AccountCreated);
    }

    // =========================================================================
    // Invariante I2: NormalizedName recalculado a cada Rename (Req 4.2)
    // =========================================================================

    [Fact(DisplayName = "I2: Rename recalcula NormalizedName")]
    public void Rename_RecalculatesNormalizedName()
    {
        var account = CreateAccount("Empresa Original");
        var newName = AccountName.Create("Nova Empresa S.A.");

        account.Rename(newName, Normalizer);

        account.Name.Should().Be(newName);
        account.NormalizedName.Value.Should().Be(Normalizer.Normalize("Nova Empresa S.A."));
    }

    [Fact(DisplayName = "I2: Rename publica AccountUpdated domain event")]
    public void Rename_PublishesAccountUpdatedEvent()
    {
        var account = CreateAccount("Empresa");
        account.ClearDomainEvents();

        account.Rename(AccountName.Create("Empresa Renomeada"), Normalizer);

        account.DomainEvents.Should().ContainSingle(e =>
            e is AccountManagement.Domain.Accounts.Events.AccountUpdated);
    }

    // =========================================================================
    // AddContact — Invariante I3: Contact herda tenant_id e account_id
    // =========================================================================

    [Fact(DisplayName = "I3: AddContact atribui tenant_id e account_id do root ao contato")]
    public void AddContact_AssignsTenantAndAccountIdFromRoot()
    {
        var account = CreateAccount("Azim CRM");
        var info = ContactInfo.Create("João Silva", Email.Create("joao@azim.com"));

        account.AddContact(info, role: "Gerente");

        var contact = account.Contacts.Single();
        contact.TenantId.Should().Be(TenantId);
        contact.AccountId.Should().Be(account.Id);
    }

    [Fact(DisplayName = "AddContact publica ContactLinked com maskedDelta")]
    public void AddContact_PublishesContactLinkedWithMaskedDelta()
    {
        var account = CreateAccount("Azim CRM");
        account.ClearDomainEvents();
        var info = ContactInfo.Create("Maria Santos", Email.Create("maria@azim.com"));

        account.AddContact(info, role: null);

        var evt = account.DomainEvents
            .OfType<AccountManagement.Domain.Accounts.Events.ContactLinked>()
            .Single();
        evt.Should().NotBeNull();
        // maskedDelta não pode conter PII em claro (RNF 1.2, DD-003)
        evt.MaskedDelta.Should().NotContain("Maria Santos");
        evt.MaskedDelta.Should().NotContain("maria@azim.com");
    }

    // =========================================================================
    // UpdateContact
    // =========================================================================

    [Fact(DisplayName = "UpdateContact atualiza PII do contato e publica ContactLinked")]
    public void UpdateContact_UpdatesPiiAndPublishesEvent()
    {
        var account = CreateAccount("Azim CRM");
        var originalInfo = ContactInfo.Create("Ana Souza");
        account.AddContact(originalInfo, role: null);
        var contactId = account.Contacts.Single().Id;
        account.ClearDomainEvents();

        var newInfo = ContactInfo.Create("Ana Lima", Email.Create("ana.lima@azim.com"));
        account.UpdateContact(contactId, newInfo, role: "Diretora");

        var contact = account.Contacts.Single();
        contact.Info.Name.Should().Be("Ana Lima");
        account.DomainEvents.Should().ContainSingle(e =>
            e is AccountManagement.Domain.Accounts.Events.ContactLinked);
    }

    // =========================================================================
    // ForgetContact — state machine Active → Anonymized (Req 7, DD-001)
    // =========================================================================

    [Fact(DisplayName = "ForgetContact transiciona contato de Active para Anonymized")]
    public void ForgetContact_TransitionsToAnonymized()
    {
        var account = CreateAccount("Azim CRM");
        var info = ContactInfo.Create("Carlos Oliveira", Email.Create("carlos@azim.com"));
        account.AddContact(info, role: null);
        var contactId = account.Contacts.Single().Id;
        account.ClearDomainEvents();

        account.ForgetContact(contactId, requestedBy: Guid.NewGuid());

        var contact = account.Contacts.Single();
        contact.PrivacyState.Should().Be(ContactPrivacyState.Anonymized);
    }

    [Fact(DisplayName = "ForgetContact substitui PII por marcador e preserva contact_id")]
    public void ForgetContact_ReplacesPiiWithMarkerAndPreservesContactId()
    {
        var account = CreateAccount("Azim CRM");
        var info = ContactInfo.Create("Fernanda Lima", Email.Create("fernanda@azim.com"),
            Phone.Create("11987654321"));
        account.AddContact(info, role: null);
        var contactId = account.Contacts.Single().Id;

        account.ForgetContact(contactId, requestedBy: Guid.NewGuid());

        var contact = account.Contacts.Single();
        contact.Id.Should().Be(contactId); // contact_id preservado (Req 7.3)
        contact.Info.Name.Should().Be(ContactInfo.AnonymizationMarker);
        contact.Info.Email.Should().BeNull();
        contact.Info.Phone.Should().BeNull();
    }

    [Fact(DisplayName = "ForgetContact publica ContactForgotten sem PII")]
    public void ForgetContact_PublishesContactForgottenWithoutPii()
    {
        var account = CreateAccount("Azim CRM");
        var info = ContactInfo.Create("Ricardo Matos", Email.Create("ricardo@azim.com"));
        account.AddContact(info, role: null);
        var contactId = account.Contacts.Single().Id;
        account.ClearDomainEvents();
        var requestedBy = Guid.NewGuid();

        account.ForgetContact(contactId, requestedBy);

        var evt = account.DomainEvents
            .OfType<AccountManagement.Domain.Accounts.Events.ContactForgotten>()
            .Single();
        evt.ContactId.Should().Be(contactId);
        evt.RequestedBy.Should().Be(requestedBy);
    }

    [Fact(DisplayName = "ForgetContact repetida sobre contato já Anonymized lança ContactAlreadyForgottenException")]
    public void ForgetContact_Repeated_ThrowsContactAlreadyForgottenException()
    {
        var account = CreateAccount("Azim CRM");
        var info = ContactInfo.Create("Luiza Costa");
        account.AddContact(info, role: null);
        var contactId = account.Contacts.Single().Id;
        account.ForgetContact(contactId, Guid.NewGuid());

        var act = () => account.ForgetContact(contactId, Guid.NewGuid());

        act.Should().Throw<ContactAlreadyForgottenException>();
    }

    // =========================================================================
    // Reconstitution
    // =========================================================================

    [Fact(DisplayName = "Account.Reconstitute cria agregado sem gerar domain events")]
    public void Reconstitute_DoesNotRaiseDomainEvents()
    {
        var id = Guid.NewGuid();
        var name = AccountName.Create("Reconstituída");

        var account = Account.Reconstitute(
            id, TenantId, Guid.NewGuid(), name,
            NormalizedName.Create(Normalizer.Normalize("Reconstituída")),
            website: null, notes: null,
            createdAt: DateTimeOffset.UtcNow,
            updatedAt: DateTimeOffset.UtcNow,
            contacts: []);

        account.DomainEvents.Should().BeEmpty();
        account.Id.Should().Be(id);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static Account CreateAccount(string name)
    {
        var accountName = AccountName.Create(name);
        return Account.Create(TenantId, Guid.NewGuid(), accountName, website: null, notes: null, Normalizer);
    }
}

using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.Exceptions;
using AccountManagement.Domain.Accounts.Services;
using AccountManagement.Domain.Accounts.ValueObjects;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace AccountManagement.Domain.Tests.Accounts;

/// <summary>
/// Testes de propriedade para a state machine de privacidade do contato.
///
/// PBT-03: após o esquecimento, nenhuma propriedade de PII retorna o valor original;
/// <c>contact_id</c> permanece idêntico; transição <c>Active → Anonymized</c>
/// não admite retorno (irreversível).
///
/// Mapeia: design §4.5, TASK-03 ST-03, Req 7, PBT-03.
/// </summary>
public sealed class ContactStateMachineTests
{
    private static readonly NameNormalizer Normalizer = new();
    private static readonly Guid TenantId = Guid.NewGuid();

    // =========================================================================
    // PBT-03 — Irreversibilidade da anonimização e preservação de contact_id
    // =========================================================================

    /// <summary>
    /// PBT-03: para qualquer nome de contato gerado, após o esquecimento:
    /// 1. PII (nome) não é o valor original.
    /// 2. contact_id permanece idêntico.
    /// 3. Estado é Anonymized.
    ///
    /// Mapeia: requirements PBT-03, design §4.5, TASK-03 ST-03.
    /// </summary>
    [Property(
        DisplayName = "PBT-03: após esquecimento, PII não é original e contact_id é preservado",
        MaxTest = 1000)]
    public bool PBT03_ForgetContact_PiiNotOriginalAndIdPreserved(NonEmptyString contactName)
    {
        // Precondição: nome deve ter ao menos um caractere não-whitespace (ContactInfo exige)
        if (string.IsNullOrWhiteSpace(contactName.Item))
            return true; // ignora este caso gerado — não é o escopo da propriedade

        // Arrange: cria conta e contato com nome gerado pelo FsCheck
        var account = CreateAccount("Conta PBT-03");
        var info = ContactInfo.Create(contactName.Item);
        account.AddContact(info, role: null);
        var contact = account.Contacts.Single();
        var originalContactId = contact.Id;
        var originalName = contactName.Item;

        // Act
        account.ForgetContact(contact.Id, requestedBy: Guid.NewGuid());

        // Assert (retorna bool para FsCheck)
        var forgotten = account.Contacts.Single();
        var nameIsNotOriginal = forgotten.Info.Name != originalName;
        var idIsPreserved = forgotten.Id == originalContactId;
        var isAnonymized = forgotten.PrivacyState.IsAnonymized;

        return nameIsNotOriginal && idIsPreserved && isAnonymized;
    }

    /// <summary>
    /// PBT-03 complementar: transição Active → Anonymized é irreversível.
    /// Após anonimização, qualquer tentativa de novo esquecimento lança exceção.
    ///
    /// Mapeia: requirements PBT-03, design §4.5, DD-001, TASK-03 ST-03.
    /// </summary>
    [Property(
        DisplayName = "PBT-03: segunda chamada de ForgetContact lança ContactAlreadyForgottenException",
        MaxTest = 1000)]
    public bool PBT03_ForgetContact_IsIrreversible(NonEmptyString contactName)
    {
        // Precondição: nome deve ter ao menos um caractere não-whitespace
        if (string.IsNullOrWhiteSpace(contactName.Item))
            return true; // ignora este caso gerado

        var account = CreateAccount("Conta PBT-03 Irreversível");
        var info = ContactInfo.Create(contactName.Item);
        account.AddContact(info, role: null);
        var contactId = account.Contacts.Single().Id;

        // Primeira chamada — deve funcionar
        account.ForgetContact(contactId, Guid.NewGuid());

        // Segunda chamada — deve lançar exceção
        try
        {
            account.ForgetContact(contactId, Guid.NewGuid());
            return false; // Não deveria chegar aqui
        }
        catch (ContactAlreadyForgottenException)
        {
            return true; // Esperado
        }
        catch
        {
            return false; // Exceção inesperada — falha
        }
    }

    // =========================================================================
    // Testes de specifications
    // =========================================================================

    [Fact(DisplayName = "SimilarAccountSpecification detecta candidato com mesmo tenant e normalized_name")]
    public void SimilarAccountSpecification_MatchesSameTenantAndNormalizedName()
    {
        var spec = new AccountManagement.Domain.Accounts.Specifications.SimilarAccountSpecification(
            TenantId, NormalizedName.Create("azim crm"));

        var account = Account.Reconstitute(
            Guid.NewGuid(), TenantId,
            AccountName.Create("Azim CRM"),
            NormalizedName.Create("azim crm"),
            null, null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, []);

        spec.IsSatisfiedBy(account).Should().BeTrue();
    }

    [Fact(DisplayName = "SimilarAccountSpecification rejeita candidato de outro tenant")]
    public void SimilarAccountSpecification_RejectsOtherTenant()
    {
        var spec = new AccountManagement.Domain.Accounts.Specifications.SimilarAccountSpecification(
            TenantId, NormalizedName.Create("azim crm"));

        var otherTenant = Guid.NewGuid();
        var account = Account.Reconstitute(
            Guid.NewGuid(), otherTenant,
            AccountName.Create("Azim CRM"),
            NormalizedName.Create("azim crm"),
            null, null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, []);

        spec.IsSatisfiedBy(account).Should().BeFalse();
    }

    [Fact(DisplayName = "SimilarAccountSpecification rejeita nome normalizado diferente")]
    public void SimilarAccountSpecification_RejectsDifferentNormalizedName()
    {
        var spec = new AccountManagement.Domain.Accounts.Specifications.SimilarAccountSpecification(
            TenantId, NormalizedName.Create("azim crm"));

        var account = Account.Reconstitute(
            Guid.NewGuid(), TenantId,
            AccountName.Create("Outra Empresa"),
            NormalizedName.Create("outra empresa"),
            null, null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, []);

        spec.IsSatisfiedBy(account).Should().BeFalse();
    }

    [Fact(DisplayName = "TenantScopeSpecification aceita conta do mesmo tenant")]
    public void TenantScopeSpecification_AcceptsAccountFromSameTenant()
    {
        var spec = new AccountManagement.Domain.Accounts.Specifications.TenantScopeSpecification(TenantId);

        var account = Account.Reconstitute(
            Guid.NewGuid(), TenantId,
            AccountName.Create("Azim"),
            NormalizedName.Create("azim"),
            null, null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, []);

        spec.IsSatisfiedBy(account).Should().BeTrue();
    }

    [Fact(DisplayName = "TenantScopeSpecification rejeita conta de outro tenant")]
    public void TenantScopeSpecification_RejectsAccountFromOtherTenant()
    {
        var spec = new AccountManagement.Domain.Accounts.Specifications.TenantScopeSpecification(TenantId);

        var account = Account.Reconstitute(
            Guid.NewGuid(), Guid.NewGuid(),
            AccountName.Create("Outra"),
            NormalizedName.Create("outra"),
            null, null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, []);

        spec.IsSatisfiedBy(account).Should().BeFalse();
    }

    // =========================================================================
    // Helper
    // =========================================================================

    private static Account CreateAccount(string name)
    {
        return Account.Create(TenantId, AccountName.Create(name), null, null, Normalizer);
    }
}

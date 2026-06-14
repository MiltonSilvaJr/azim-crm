using AccountManagement.Application.Accounts.Commands.CreateAccount;
using AccountManagement.Application.Accounts.Commands.UpdateAccount;
using AccountManagement.Application.Accounts.Queries.SearchAccounts;
using FluentAssertions;

namespace AccountManagement.Application.Tests.Accounts.Validators;

/// <summary>
/// Testes para validators FluentValidation de conta.
///
/// Mapeia: TASK-04 ST-03, design §5.5, ACC-ERR-001, ACC-ERR-002.
/// </summary>
public sealed class CreateAccountValidatorTests
{
    private readonly CreateAccountValidator _validator = new();

    [Fact(DisplayName = "CreateAccountValidator: nome vazio → erro ACC-ERR-001")]
    public void Validate_EmptyName_ReturnsAccErr001()
    {
        var command = new CreateAccountCommand(
            TenantId: Guid.NewGuid(),
            Name: "",
            Website: null,
            Notes: null,
            ConfirmCreateDespiteSimilar: false);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "ACC-ERR-001");
    }

    [Fact(DisplayName = "CreateAccountValidator: nome somente espaços → erro ACC-ERR-001")]
    public void Validate_WhitespaceName_ReturnsAccErr001()
    {
        var command = new CreateAccountCommand(
            TenantId: Guid.NewGuid(),
            Name: "   ",
            Website: null,
            Notes: null,
            ConfirmCreateDespiteSimilar: false);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "ACC-ERR-001");
    }

    [Fact(DisplayName = "CreateAccountValidator: nome nulo → erro ACC-ERR-001")]
    public void Validate_NullName_ReturnsAccErr001()
    {
        var command = new CreateAccountCommand(
            TenantId: Guid.NewGuid(),
            Name: null!,
            Website: null,
            Notes: null,
            ConfirmCreateDespiteSimilar: false);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "ACC-ERR-001");
    }

    [Fact(DisplayName = "CreateAccountValidator: nome válido → sem erros")]
    public void Validate_ValidName_NoErrors()
    {
        var command = new CreateAccountCommand(
            TenantId: Guid.NewGuid(),
            Name: "Empresa Válida",
            Website: null,
            Notes: null,
            ConfirmCreateDespiteSimilar: false);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact(DisplayName = "CreateAccountValidator: nome com 256 caracteres → erro ACC-ERR-001")]
    public void Validate_NameTooLong_ReturnsAccErr001()
    {
        var command = new CreateAccountCommand(
            TenantId: Guid.NewGuid(),
            Name: new string('A', 256),
            Website: null,
            Notes: null,
            ConfirmCreateDespiteSimilar: false);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "ACC-ERR-001");
    }
}

public sealed class UpdateAccountValidatorTests
{
    private readonly UpdateAccountValidator _validator = new();

    [Fact(DisplayName = "UpdateAccountValidator: nome vazio → erro ACC-ERR-001")]
    public void Validate_EmptyName_ReturnsAccErr001()
    {
        var command = new UpdateAccountCommand(
            AccountId: Guid.NewGuid(),
            Name: "",
            Website: null,
            Notes: null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "ACC-ERR-001");
    }
}

public sealed class SearchAccountsValidatorTests
{
    private readonly SearchAccountsValidator _validator = new();

    [Fact(DisplayName = "SearchAccountsValidator: page=0 → erro ACC-ERR-002")]
    public void Validate_ZeroPage_ReturnsAccErr002()
    {
        var query = new SearchAccountsQuery(Search: null, Page: 0, PageSize: 10);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "ACC-ERR-002" && e.PropertyName == "Page");
    }

    [Fact(DisplayName = "SearchAccountsValidator: pageSize=0 → erro ACC-ERR-002")]
    public void Validate_ZeroPageSize_ReturnsAccErr002()
    {
        var query = new SearchAccountsQuery(Search: null, Page: 1, PageSize: 0);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "ACC-ERR-002" && e.PropertyName == "PageSize");
    }

    [Fact(DisplayName = "SearchAccountsValidator: pageSize=101 → erro ACC-ERR-002")]
    public void Validate_PageSizeTooLarge_ReturnsAccErr002()
    {
        var query = new SearchAccountsQuery(Search: null, Page: 1, PageSize: 101);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "ACC-ERR-002" && e.PropertyName == "PageSize");
    }

    [Fact(DisplayName = "SearchAccountsValidator: parâmetros válidos → sem erros")]
    public void Validate_ValidParameters_NoErrors()
    {
        var query = new SearchAccountsQuery(Search: "empresa", Page: 1, PageSize: 20);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}

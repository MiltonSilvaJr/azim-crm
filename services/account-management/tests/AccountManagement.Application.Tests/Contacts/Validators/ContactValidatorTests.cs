using AccountManagement.Application.Contacts.Commands.CreateContact;
using AccountManagement.Application.Contacts.Commands.UpdateContact;
using FluentAssertions;

namespace AccountManagement.Application.Tests.Contacts.Validators;

/// <summary>
/// Testes para validators de contato.
///
/// Mapeia: TASK-06 ST-01, design §5.5, ACC-ERR-004, ACC-ERR-005.
/// </summary>
public sealed class CreateContactValidatorTests
{
    private readonly CreateContactValidator _validator = new();

    [Fact(DisplayName = "CreateContactValidator: e-mail inválido → erro ACC-ERR-004")]
    public void Validate_InvalidEmail_ReturnsAccErr004()
    {
        var command = new CreateContactCommand(
            AccountId: Guid.NewGuid(),
            Name: "João",
            Email: "email-invalido",
            Phone: null,
            Role: null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "ACC-ERR-004");
    }

    [Fact(DisplayName = "CreateContactValidator: nome vazio → erro ACC-ERR-005")]
    public void Validate_EmptyName_ReturnsAccErr005()
    {
        var command = new CreateContactCommand(
            AccountId: Guid.NewGuid(),
            Name: "",
            Email: null,
            Phone: null,
            Role: null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "ACC-ERR-005");
    }

    [Fact(DisplayName = "CreateContactValidator: dados válidos → sem erros")]
    public void Validate_ValidData_NoErrors()
    {
        var command = new CreateContactCommand(
            AccountId: Guid.NewGuid(),
            Name: "Maria Silva",
            Email: "maria@empresa.com",
            Phone: "11987654321",
            Role: "Diretora");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact(DisplayName = "CreateContactValidator: e-mail nulo → válido (e-mail é opcional)")]
    public void Validate_NullEmail_IsValid()
    {
        var command = new CreateContactCommand(
            AccountId: Guid.NewGuid(),
            Name: "Pedro Santos",
            Email: null,
            Phone: null,
            Role: null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}

public sealed class UpdateContactValidatorTests
{
    private readonly UpdateContactValidator _validator = new();

    [Fact(DisplayName = "UpdateContactValidator: e-mail inválido → erro ACC-ERR-004")]
    public void Validate_InvalidEmail_ReturnsAccErr004()
    {
        var command = new UpdateContactCommand(
            AccountId: Guid.NewGuid(),
            ContactId: Guid.NewGuid(),
            Name: "João",
            Email: "nao-e-email",
            Phone: null,
            Role: null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "ACC-ERR-004");
    }

    [Fact(DisplayName = "UpdateContactValidator: nome vazio → erro ACC-ERR-005")]
    public void Validate_EmptyName_ReturnsAccErr005()
    {
        var command = new UpdateContactCommand(
            AccountId: Guid.NewGuid(),
            ContactId: Guid.NewGuid(),
            Name: "  ",
            Email: null,
            Phone: null,
            Role: null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "ACC-ERR-005");
    }
}

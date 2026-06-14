using FluentValidation;

namespace AccountManagement.Application.Contacts.Commands.CreateContact;

/// <summary>
/// Validator FluentValidation para <see cref="CreateContactCommand"/>.
///
/// Valida a sintaxe do e-mail antes do domínio para retornar código de erro padronizado.
/// O domínio valida mais profundamente via <c>Email.Create()</c>.
///
/// Mapeia: design §5.5, Req 5.4, ACC-ERR-004, ACC-ERR-005.
/// </summary>
public sealed class CreateContactValidator : AbstractValidator<CreateContactCommand>
{
    /// <summary>Inicializa as regras de validação do command.</summary>
    public CreateContactValidator()
    {
        RuleFor(c => c.AccountId)
            .NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty()
            .WithErrorCode("ACC-ERR-005")
            .WithMessage("O nome do contato é obrigatório.");

        RuleFor(c => c.Email)
            .EmailAddress()
            .When(c => !string.IsNullOrEmpty(c.Email))
            .WithErrorCode("ACC-ERR-004")
            .WithMessage("O e-mail do contato é inválido.");
    }
}

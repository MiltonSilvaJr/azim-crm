using FluentValidation;

namespace AccountManagement.Application.Contacts.Commands.UpdateContact;

/// <summary>
/// Validator FluentValidation para <see cref="UpdateContactCommand"/>.
///
/// Valida a sintaxe do e-mail antes do domínio para retornar código de erro padronizado.
/// O domínio valida mais profundamente via <c>Email.Create()</c>.
///
/// Mapeia: design §5.5, Req 5, ACC-ERR-004, ACC-ERR-005, ACC-ERR-006.
/// </summary>
public sealed class UpdateContactValidator : AbstractValidator<UpdateContactCommand>
{
    /// <summary>Inicializa as regras de validação do command.</summary>
    public UpdateContactValidator()
    {
        RuleFor(c => c.AccountId)
            .NotEmpty();

        RuleFor(c => c.ContactId)
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

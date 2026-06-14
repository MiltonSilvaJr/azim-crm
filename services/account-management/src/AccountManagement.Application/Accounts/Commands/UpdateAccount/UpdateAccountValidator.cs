using AccountManagement.Domain.Accounts.ValueObjects;
using FluentValidation;

namespace AccountManagement.Application.Accounts.Commands.UpdateAccount;

/// <summary>
/// Validator FluentValidation para <see cref="UpdateAccountCommand"/>.
///
/// Mapeia: design §5.5, Req 4.2, ACC-ERR-001.
/// </summary>
public sealed class UpdateAccountValidator : AbstractValidator<UpdateAccountCommand>
{
    /// <summary>Inicializa as regras de validação do command.</summary>
    public UpdateAccountValidator()
    {
        RuleFor(c => c.AccountId)
            .NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty()
            .WithErrorCode("ACC-ERR-001")
            .WithMessage("O nome da conta é obrigatório.")
            .MaximumLength(AccountName.MaxLength)
            .WithErrorCode("ACC-ERR-001")
            .WithMessage($"O nome da conta não pode exceder {AccountName.MaxLength} caracteres.");
    }
}

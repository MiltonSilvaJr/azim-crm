using AccountManagement.Domain.Accounts.ValueObjects;
using FluentValidation;

namespace AccountManagement.Application.Accounts.Commands.CreateAccount;

/// <summary>
/// Validator FluentValidation para <see cref="CreateAccountCommand"/>.
///
/// Validação sintática na borda da camada Application (design §5.5, rule api-and-contracts.md).
/// As regras de negócio mais profundas (invariantes I1/I2) são verificadas no domínio.
///
/// Mapeia: design §5.5, Req 1.5, ACC-ERR-001.
/// </summary>
public sealed class CreateAccountValidator : AbstractValidator<CreateAccountCommand>
{
    /// <summary>Inicializa as regras de validação do command.</summary>
    public CreateAccountValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty()
            .WithErrorCode("ACC-ERR-001")
            .WithMessage("O nome da conta é obrigatório.")
            .MaximumLength(AccountName.MaxLength)
            .WithErrorCode("ACC-ERR-001")
            .WithMessage($"O nome da conta não pode exceder {AccountName.MaxLength} caracteres.");

        RuleFor(c => c.TenantId)
            .NotEmpty();
    }
}

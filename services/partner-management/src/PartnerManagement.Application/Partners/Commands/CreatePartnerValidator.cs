using FluentValidation;

namespace PartnerManagement.Application.Partners.Commands;

/// <summary>
/// Validador sintático de <see cref="CreatePartnerCommand"/>.
/// Executa validação de borda antes que o handler seja invocado (ValidationBehavior).
/// Regras de negócio (invariantes) ficam no domínio.
/// Mapeia: design §5.5, PM-ERR-001..004.
/// </summary>
public sealed class CreatePartnerValidator : AbstractValidator<CreatePartnerCommand>
{
    /// <summary>Inicializa as regras de validação.</summary>
    public CreatePartnerValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithErrorCode(PartnerErrors.NameRequired)
            .WithMessage("TenantId é obrigatório.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithErrorCode(PartnerErrors.NameRequired)
            .WithMessage("Nome do parceiro é obrigatório.")
            .MaximumLength(255)
            .WithErrorCode(PartnerErrors.NameRequired)
            .WithMessage("Nome do parceiro não pode exceder 255 caracteres.");

        RuleFor(x => x.Role)
            .NotEmpty()
            .WithErrorCode(PartnerErrors.InvalidRole)
            .WithMessage("Papel de parceiro é obrigatório.");

        RuleFor(x => x.CommissionDefaults)
            .NotNull()
            .WithErrorCode(PartnerErrors.PercentageOutOfRange)
            .WithMessage("Percentuais padrão são obrigatórios.");

        RuleFor(x => x.CreatedBy)
            .NotEmpty()
            .WithMessage("Autor da criação é obrigatório.");

        // E-mail: validação sintática de formato (regra de domínio valida formato completo)
        When(x => x.ContactEmail is not null, () =>
        {
            RuleFor(x => x.ContactEmail!)
                .EmailAddress()
                .WithErrorCode(PartnerErrors.InvalidContactEmail)
                .WithMessage("E-mail de contato em formato inválido.");
        });
    }
}

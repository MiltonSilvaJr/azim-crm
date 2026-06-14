using FluentValidation;

namespace PartnerManagement.Application.Partners.Commands;

/// <summary>
/// Validador sintático de <see cref="UpdatePartnerCommand"/>.
/// Executa validação de borda antes que o handler seja invocado (ValidationBehavior).
/// Mapeia: design §5.5, PM-ERR-001..004, PM-ERR-007.
/// </summary>
public sealed class UpdatePartnerValidator : AbstractValidator<UpdatePartnerCommand>
{
    /// <summary>Inicializa as regras de validação.</summary>
    public UpdatePartnerValidator()
    {
        RuleFor(x => x.PartnerId)
            .NotEmpty()
            .WithErrorCode(PartnerErrors.PartnerNotFound)
            .WithMessage("PartnerId é obrigatório.");

        RuleFor(x => x.TenantId)
            .NotEmpty()
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

        RuleFor(x => x.UpdatedBy)
            .NotEmpty()
            .WithMessage("Autor da atualização é obrigatório.");

        When(x => x.ContactEmail is not null, () =>
        {
            RuleFor(x => x.ContactEmail!)
                .EmailAddress()
                .WithErrorCode(PartnerErrors.InvalidContactEmail)
                .WithMessage("E-mail de contato em formato inválido.");
        });
    }
}

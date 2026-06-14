using FluentValidation;

namespace GoalForecast.Application.Commands;

/// <summary>
/// Validação sintática de <see cref="CreateOrUpdateGoalCommand"/> na borda de aplicação.
/// Erros mapeados para códigos GF-ERR-001/002/003 (catálogo design §12).
///
/// Regras (design §5.5):
/// <list type="bullet">
///   <item>month ∈ [1..12] → GF-ERR-002</item>
///   <item>year quatro dígitos [1000..9999] → GF-ERR-002</item>
///   <item>valorMeta >= 0 inteiro → GF-ERR-001</item>
///   <item>coerência scope × ownerId → GF-ERR-003</item>
/// </list>
///
/// Mapeia: design §5.5, TASK-10.
/// </summary>
public sealed class CreateOrUpdateGoalCommandValidator
    : AbstractValidator<CreateOrUpdateGoalCommand>
{
    /// <summary>Configura todas as regras de validação sintática.</summary>
    public CreateOrUpdateGoalCommandValidator()
    {
        RuleFor(c => c.Month)
            .InclusiveBetween(1, 12)
            .WithErrorCode("GF-ERR-002")
            .WithMessage("Mês ou ano fora da faixa: Month deve estar entre 1 e 12.");

        RuleFor(c => c.Year)
            .InclusiveBetween(1000, 9999)
            .WithErrorCode("GF-ERR-002")
            .WithMessage("Mês ou ano fora da faixa: Year deve ter quatro dígitos.");

        RuleFor(c => c.ValorMeta)
            .GreaterThanOrEqualTo(0L)
            .WithErrorCode("GF-ERR-001")
            .WithMessage("Valor de meta inválido: valorMeta não pode ser negativo.");

        RuleFor(c => c.BuId)
            .NotEmpty()
            .WithErrorCode("GF-ERR-003")
            .WithMessage("Escopo inconsistente: buId é obrigatório.");

        RuleFor(c => c.Scope)
            .Must(s => s == "BU" || s == "RESPONSAVEL")
            .WithErrorCode("GF-ERR-003")
            .WithMessage("Escopo inconsistente: Scope deve ser 'BU' ou 'RESPONSAVEL'.");

        // Coerência scope × ownerId: RESPONSAVEL exige ownerId; BU proíbe ownerId.
        When(c => c.Scope == "RESPONSAVEL", () =>
        {
            RuleFor(c => c.OwnerId)
                .NotNull()
                .WithErrorCode("GF-ERR-003")
                .WithMessage("Escopo inconsistente: OwnerId é obrigatório no escopo RESPONSAVEL.")
                .DependentRules(() =>
                {
                    RuleFor(c => c.OwnerId)
                        .NotEqual(Guid.Empty)
                        .WithErrorCode("GF-ERR-003")
                        .WithMessage("Escopo inconsistente: OwnerId não pode ser Guid.Empty.");
                });
        });

        When(c => c.Scope == "BU", () =>
        {
            RuleFor(c => c.OwnerId)
                .Null()
                .WithErrorCode("GF-ERR-003")
                .WithMessage("Escopo inconsistente: OwnerId não deve ser informado no escopo BU.");
        });
    }
}

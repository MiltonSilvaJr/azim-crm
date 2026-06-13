using FluentValidation;
using Organization.Application.Abstractions;

namespace Organization.Application.Commands.BusinessUnit;

/// <summary>
/// Command para criar uma Business Unit com seeds de estágios, canais de origem e motivos de perda.
/// Autorizado apenas para <c>TAdmin</c>.
/// </summary>
/// <param name="Name">Nome da Business Unit (1..120 caracteres).</param>
[RequiresRole("TAdmin")]
public sealed record CreateBusinessUnitCommand(string Name) : ICommand<Guid>;

/// <summary>Validador sintático do <see cref="CreateBusinessUnitCommand"/>.</summary>
public sealed class CreateBusinessUnitCommandValidator : AbstractValidator<CreateBusinessUnitCommand>
{
    /// <summary>Inicializa as regras de validação.</summary>
    public CreateBusinessUnitCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome da Business Unit é obrigatório.")
            .MaximumLength(120).WithMessage("O nome da Business Unit não pode exceder 120 caracteres.");
    }
}

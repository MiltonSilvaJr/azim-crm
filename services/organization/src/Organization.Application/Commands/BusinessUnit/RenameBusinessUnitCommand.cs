using FluentValidation;
using Organization.Application.Abstractions;

namespace Organization.Application.Commands.BusinessUnit;

/// <summary>
/// Command para renomear uma Business Unit existente.
/// Autorizado para <c>TAdmin</c> (qualquer BU) e <c>GestorBU</c> (apenas a própria BU).
/// </summary>
/// <param name="BusinessUnitId">Identificador da BU a renomear.</param>
/// <param name="NewName">Novo nome da BU (1..120 caracteres).</param>
[RequiresRole("TAdmin", "GestorBU", ScopedToBu = true)]
public sealed record RenameBusinessUnitCommand(Guid BusinessUnitId, string NewName) : ICommand;

/// <summary>Validador sintático do <see cref="RenameBusinessUnitCommand"/>.</summary>
public sealed class RenameBusinessUnitCommandValidator : AbstractValidator<RenameBusinessUnitCommand>
{
    /// <summary>Inicializa as regras de validação.</summary>
    public RenameBusinessUnitCommandValidator()
    {
        RuleFor(x => x.BusinessUnitId)
            .NotEmpty().WithMessage("O identificador da Business Unit é obrigatório.");

        RuleFor(x => x.NewName)
            .NotEmpty().WithMessage("O novo nome da Business Unit é obrigatório.")
            .MaximumLength(120).WithMessage("O nome da Business Unit não pode exceder 120 caracteres.");
    }
}

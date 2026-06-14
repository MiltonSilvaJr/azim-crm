using FluentValidation;
using Organization.Application.Abstractions;

namespace Organization.Application.Commands.BusinessUnit;

/// <summary>
/// Command para desativar (soft-delete) uma Business Unit.
/// Bloqueado quando há oportunidades ativas (ORG-ERR-002).
/// Autorizado apenas para <c>TAdmin</c>.
/// </summary>
/// <param name="BusinessUnitId">Identificador da BU a desativar.</param>
[RequiresRole("TAdmin")]
public sealed record DeactivateBusinessUnitCommand(Guid BusinessUnitId) : ICommand;

/// <summary>Validador sintático do <see cref="DeactivateBusinessUnitCommand"/>.</summary>
public sealed class DeactivateBusinessUnitCommandValidator : AbstractValidator<DeactivateBusinessUnitCommand>
{
    /// <summary>Inicializa as regras de validação.</summary>
    public DeactivateBusinessUnitCommandValidator()
    {
        RuleFor(x => x.BusinessUnitId)
            .NotEmpty().WithMessage("O identificador da Business Unit é obrigatório.");
    }
}

using FluentValidation;
using Organization.Application.Abstractions;

namespace Organization.Application.Commands.PipelineConfig;

/// <summary>
/// Command para adicionar um motivo de perda a uma Business Unit.
/// Autorizado para <c>TAdmin</c>.
/// </summary>
/// <param name="BuId">Identificador da Business Unit.</param>
/// <param name="ReasonId">Identificador pré-gerado do motivo.</param>
/// <param name="Name">Nome do motivo de perda.</param>
[RequiresRole("TAdmin")]
public sealed record AddLossReasonCommand(Guid BuId, Guid ReasonId, string Name) : ICommand;

/// <summary>Validador sintático do <see cref="AddLossReasonCommand"/>.</summary>
public sealed class AddLossReasonCommandValidator : AbstractValidator<AddLossReasonCommand>
{
    /// <summary>Inicializa as regras de validação.</summary>
    public AddLossReasonCommandValidator()
    {
        RuleFor(x => x.BuId).NotEmpty();
        RuleFor(x => x.ReasonId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

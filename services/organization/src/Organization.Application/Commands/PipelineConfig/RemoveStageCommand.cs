using FluentValidation;
using Organization.Application.Abstractions;

namespace Organization.Application.Commands.PipelineConfig;

/// <summary>
/// Command para remover um estágio de pipeline de uma Business Unit.
/// Valida <c>TerminalStagesPolicy</c> no domínio (ORG-ERR-015).
/// Autorizado para <c>TAdmin</c> e <c>GestorBU</c> (com escopo na BU).
/// </summary>
/// <param name="BuId">Identificador da Business Unit.</param>
/// <param name="StageId">Identificador do estágio a remover.</param>
[RequiresRole("TAdmin", "GestorBU", ScopedToBu = true)]
public sealed record RemoveStageCommand(Guid BuId, Guid StageId) : ICommand;

/// <summary>Validador sintático do <see cref="RemoveStageCommand"/>.</summary>
public sealed class RemoveStageCommandValidator : AbstractValidator<RemoveStageCommand>
{
    /// <summary>Inicializa as regras de validação.</summary>
    public RemoveStageCommandValidator()
    {
        RuleFor(x => x.BuId).NotEmpty();
        RuleFor(x => x.StageId).NotEmpty();
    }
}

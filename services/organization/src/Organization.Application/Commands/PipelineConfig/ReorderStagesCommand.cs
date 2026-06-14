using FluentValidation;
using Organization.Application.Abstractions;

namespace Organization.Application.Commands.PipelineConfig;

/// <summary>
/// Command para reordenar estágios de pipeline em uma Business Unit.
/// Valida unicidade de posições resultantes no domínio (ORG-ERR-014).
/// Autorizado para <c>TAdmin</c> e <c>GestorBU</c> (com escopo na BU).
/// </summary>
/// <param name="BuId">Identificador da Business Unit.</param>
/// <param name="NewPositions">Mapa de stageId → nova posição.</param>
[RequiresRole("TAdmin", "GestorBU", ScopedToBu = true)]
public sealed record ReorderStagesCommand(Guid BuId, IReadOnlyDictionary<Guid, int> NewPositions) : ICommand;

/// <summary>Validador sintático do <see cref="ReorderStagesCommand"/>.</summary>
public sealed class ReorderStagesCommandValidator : AbstractValidator<ReorderStagesCommand>
{
    /// <summary>Inicializa as regras de validação.</summary>
    public ReorderStagesCommandValidator()
    {
        RuleFor(x => x.BuId).NotEmpty();
        RuleFor(x => x.NewPositions).NotNull().NotEmpty();
        RuleForEach(x => x.NewPositions)
            .Must(kv => kv.Key != Guid.Empty && kv.Value > 0)
            .WithMessage("Cada entrada deve ter um stageId válido e posição maior que zero.");
    }
}

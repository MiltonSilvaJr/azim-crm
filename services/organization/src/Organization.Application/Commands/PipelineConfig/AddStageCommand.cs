using FluentValidation;
using Organization.Application.Abstractions;

namespace Organization.Application.Commands.PipelineConfig;

/// <summary>
/// Command para adicionar um estágio de pipeline a uma Business Unit.
/// Valida unicidade de nome (ORG-ERR-013) e posição (ORG-ERR-014) no domínio.
/// Autorizado para <c>TAdmin</c> e <c>GestorBU</c> (com escopo na BU).
/// </summary>
/// <param name="BuId">Identificador da Business Unit.</param>
/// <param name="StageId">Identificador pré-gerado do estágio.</param>
/// <param name="Name">Nome do estágio.</param>
/// <param name="ProbabilityPercent">Probabilidade de fechamento (0–100).</param>
/// <param name="Category">Categoria: open, won ou lost.</param>
/// <param name="Position">Posição na ordenação do pipeline.</param>
[RequiresRole("TAdmin", "GestorBU", ScopedToBu = true)]
public sealed record AddStageCommand(
    Guid BuId,
    Guid StageId,
    string Name,
    int ProbabilityPercent,
    string Category,
    int Position) : ICommand;

/// <summary>Validador sintático do <see cref="AddStageCommand"/>.</summary>
public sealed class AddStageCommandValidator : AbstractValidator<AddStageCommand>
{
    /// <summary>Inicializa as regras de validação.</summary>
    public AddStageCommandValidator()
    {
        RuleFor(x => x.BuId).NotEmpty();
        RuleFor(x => x.StageId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ProbabilityPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.Category)
            .NotEmpty()
            .Must(c => new[] { "open", "won", "lost" }.Contains(c))
            .WithMessage("Categoria inválida. Valores aceitos: open, won, lost.");
        RuleFor(x => x.Position).GreaterThan(0);
    }
}

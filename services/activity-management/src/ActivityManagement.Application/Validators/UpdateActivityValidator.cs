namespace ActivityManagement.Application.Validators;

using ActivityManagement.Application.Activities.Commands;
using FluentValidation;

/// <summary>
/// Validação sintática do <see cref="UpdateActivityCommand"/> (design §5.5).
/// Mapeia: ACT-ERR-001, ACT-ERR-002, design §5.5, TASK-07.
/// </summary>
public sealed class UpdateActivityValidator : AbstractValidator<UpdateActivityCommand>
{
    private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "meeting", "follow_up", "call", "email", "task"
    };

    private static readonly HashSet<string> ValidPriorities = new(StringComparer.OrdinalIgnoreCase)
    {
        "low", "medium", "high"
    };

    /// <summary>Inicializa as regras de validação.</summary>
    public UpdateActivityValidator()
    {
        RuleFor(c => c.ActivityId)
            .NotEqual(Guid.Empty)
            .WithMessage("ID da atividade é obrigatório.");

        // ACT-ERR-001
        RuleFor(c => c.Title)
            .NotEmpty()
            .WithErrorCode("ACT-ERR-001")
            .WithMessage("Título da atividade é obrigatório.");

        // ACT-ERR-010
        RuleFor(c => c.DueAt)
            .NotEqual(default(DateTimeOffset))
            .WithErrorCode("ACT-ERR-010")
            .WithMessage("Data de vencimento é obrigatória.");

        // Prioridade quando informada
        When(c => c.Priority is not null, () =>
        {
            RuleFor(c => c.Priority!)
                .Must(p => ValidPriorities.Contains(p))
                .WithMessage("Prioridade inválida. Use: low, medium ou high.");
        });
    }
}

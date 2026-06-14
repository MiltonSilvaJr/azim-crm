namespace ActivityManagement.Application.Validators;

using ActivityManagement.Application.Activities.Commands;
using FluentValidation;

/// <summary>
/// Validação sintática do <see cref="CreateActivityCommand"/> (design §5.5).
/// Rejeita o request antes do handler quando inválido (via <c>ValidationBehavior</c>).
/// Não acessa portas de leitura — apenas valida formato e presença de campos obrigatórios.
/// Validação de vínculos (oportunidade/conta) é feita no handler.
/// Mapeia: ACT-ERR-001, ACT-ERR-002, ACT-ERR-010, design §5.5, TASK-07.
/// </summary>
public sealed class CreateActivityValidator : AbstractValidator<CreateActivityCommand>
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
    public CreateActivityValidator()
    {
        // ACT-ERR-001: título obrigatório e não em branco
        RuleFor(c => c.Title)
            .NotEmpty()
            .WithErrorCode("ACT-ERR-001")
            .WithMessage("Título da atividade é obrigatório.");

        // ACT-ERR-002: tipo dentro da lista canônica
        RuleFor(c => c.Type)
            .NotEmpty()
            .WithErrorCode("ACT-ERR-002")
            .WithMessage("Tipo de atividade inválido.")
            .Must(t => ValidTypes.Contains(t))
            .When(c => !string.IsNullOrEmpty(c.Type))
            .WithErrorCode("ACT-ERR-002")
            .WithMessage("Tipo de atividade inválido. Use: meeting, follow_up, call, email ou task.");

        // ACT-ERR-010: due_at obrigatório
        RuleFor(c => c.DueAt)
            .NotEqual(default(DateTimeOffset))
            .WithErrorCode("ACT-ERR-010")
            .WithMessage("Data de vencimento é obrigatória.");

        // OwnerId obrigatório
        RuleFor(c => c.OwnerId)
            .NotEqual(Guid.Empty)
            .WithMessage("Responsável da atividade é obrigatório.");

        // Prioridade quando informada deve ser válida
        When(c => c.Priority is not null, () =>
        {
            RuleFor(c => c.Priority!)
                .Must(p => ValidPriorities.Contains(p))
                .WithMessage("Prioridade inválida. Use: low, medium ou high.");
        });
    }
}

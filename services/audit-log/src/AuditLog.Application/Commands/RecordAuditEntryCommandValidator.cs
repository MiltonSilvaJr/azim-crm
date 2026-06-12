using AuditLog.Domain.ValueObjects;
using FluentValidation;

namespace AuditLog.Application.Commands;

/// <summary>
/// Validador do <see cref="RecordAuditEntryCommand"/> na borda da camada Application.
/// Aplica validações sintáticas conforme design §5.5.
/// </summary>
public sealed class RecordAuditEntryCommandValidator : AbstractValidator<RecordAuditEntryCommand>
{
    /// <summary>Inicializa o validador com todas as regras de validação do command.</summary>
    public RecordAuditEntryCommandValidator()
    {
        // ActorId: não vazio (REQ-002.2)
        RuleFor(x => x.ActorId)
            .NotEmpty()
            .WithMessage("O identificador do autor (user_id) não pode ser vazio (REQ-002.2).");

        // EntityType: não vazio, máx. 50 chars (REQ-002.1)
        RuleFor(x => x.EntityType)
            .NotEmpty()
            .WithMessage("O tipo de entidade (entity_type) não pode ser vazio (REQ-002.1).")
            .MaximumLength(50)
            .WithMessage("O tipo de entidade (entity_type) não pode exceder 50 caracteres.");

        // EntityId: não vazio
        RuleFor(x => x.EntityId)
            .NotEmpty()
            .WithMessage("O identificador da entidade (entity_id) não pode ser vazio.");

        // Action: deve ser valor canônico válido (REQ-002.3)
        RuleFor(x => x.Action)
            .IsInEnum()
            .WithMessage("O campo action deve ser um dos valores canônicos: Create, Update ou Delete (REQ-002.3).");

        // Para Create: RawAfter obrigatório
        When(x => x.Action == AuditAction.Create, () =>
        {
            RuleFor(x => x.RawAfter)
                .NotNull()
                .WithMessage("Para action=Create, o estado posterior (RawAfter) é obrigatório.")
                .Must(d => d != null && d.Count > 0)
                .WithMessage("Para action=Create, o estado posterior (RawAfter) não pode ser vazio.");
        });

        // Para Delete: RawBefore obrigatório
        When(x => x.Action == AuditAction.Delete, () =>
        {
            RuleFor(x => x.RawBefore)
                .NotNull()
                .WithMessage("Para action=Delete, o estado anterior (RawBefore) é obrigatório.")
                .Must(d => d != null && d.Count > 0)
                .WithMessage("Para action=Delete, o estado anterior (RawBefore) não pode ser vazio.");
        });

        // Para Update: ambos obrigatórios
        When(x => x.Action == AuditAction.Update, () =>
        {
            RuleFor(x => x.RawBefore)
                .NotNull()
                .WithMessage("Para action=Update, o estado anterior (RawBefore) é obrigatório.");

            RuleFor(x => x.RawAfter)
                .NotNull()
                .WithMessage("Para action=Update, o estado posterior (RawAfter) é obrigatório.");
        });
    }
}

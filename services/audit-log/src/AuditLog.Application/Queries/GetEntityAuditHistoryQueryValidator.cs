using AuditLog.Application.Errors;
using FluentValidation;

namespace AuditLog.Application.Queries;

/// <summary>
/// Validador da <see cref="GetEntityAuditHistoryQuery"/>.
/// Verifica campos obrigatórios, limites de paginação e coerência de intervalo (design §5.5).
/// </summary>
public sealed class GetEntityAuditHistoryQueryValidator : AbstractValidator<GetEntityAuditHistoryQuery>
{
    /// <summary>Inicializa o validador com todas as regras.</summary>
    public GetEntityAuditHistoryQueryValidator()
    {
        RuleFor(x => x.EntityType)
            .NotEmpty()
            .WithMessage("O tipo de entidade (entity_type) é obrigatório.")
            .MaximumLength(50)
            .WithMessage("O tipo de entidade não pode exceder 50 caracteres.");

        RuleFor(x => x.EntityId)
            .NotEmpty()
            .WithMessage("O identificador de entidade (entity_id) é obrigatório.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("O número de página deve ser maior ou igual a 1.");

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1)
            .WithMessage("O tamanho da página deve ser maior ou igual a 1.")
            .LessThanOrEqualTo(ListAuditLogsQueryValidator.MaxPageSize)
            .WithMessage($"O tamanho da página não pode exceder {ListAuditLogsQueryValidator.MaxPageSize} registros ({AuditErrorCodes.InvalidFilter}).");

        When(x => x.From.HasValue && x.To.HasValue, () =>
        {
            RuleFor(x => x)
                .Must(x => x.From!.Value <= x.To!.Value)
                .WithName("DateRange")
                .WithMessage($"O início do intervalo (from) não pode ser posterior ao fim (to) ({AuditErrorCodes.InvalidDateRange}).");
        });
    }
}

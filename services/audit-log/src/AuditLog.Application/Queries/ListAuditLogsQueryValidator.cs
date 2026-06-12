using AuditLog.Application.Errors;
using FluentValidation;

namespace AuditLog.Application.Queries;

/// <summary>
/// Validador da <see cref="ListAuditLogsQuery"/> na borda da camada Application.
/// Verifica limites de paginação e coerência de intervalo de datas (design §5.5).
/// </summary>
public sealed class ListAuditLogsQueryValidator : AbstractValidator<ListAuditLogsQuery>
{
    /// <summary>Tamanho máximo de página permitido (REQ-007.4).</summary>
    public const int MaxPageSize = 200;

    /// <summary>Inicializa o validador com todas as regras.</summary>
    public ListAuditLogsQueryValidator()
    {
        // Paginação
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("O número de página deve ser maior ou igual a 1.");

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1)
            .WithMessage("O tamanho da página deve ser maior ou igual a 1.")
            .LessThanOrEqualTo(MaxPageSize)
            .WithMessage($"O tamanho da página não pode exceder {MaxPageSize} registros ({AuditErrorCodes.InvalidFilter}).");

        // Intervalo de datas: from não pode ser posterior a to (AUD-ERR-006)
        When(x => x.From.HasValue && x.To.HasValue, () =>
        {
            RuleFor(x => x)
                .Must(x => x.From!.Value <= x.To!.Value)
                .WithName("DateRange")
                .WithMessage($"O início do intervalo (from) não pode ser posterior ao fim (to) ({AuditErrorCodes.InvalidDateRange}).");
        });
    }
}

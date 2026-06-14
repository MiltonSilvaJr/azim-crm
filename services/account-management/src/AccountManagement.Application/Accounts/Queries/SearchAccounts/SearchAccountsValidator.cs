using FluentValidation;

namespace AccountManagement.Application.Accounts.Queries.SearchAccounts;

/// <summary>
/// Validator FluentValidation para <see cref="SearchAccountsQuery"/>.
///
/// Mapeia: design §5.5, Req 3, ACC-ERR-002.
/// </summary>
public sealed class SearchAccountsValidator : AbstractValidator<SearchAccountsQuery>
{
    /// <summary>Tamanho máximo de página permitido.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Inicializa as regras de validação da query.</summary>
    public SearchAccountsValidator()
    {
        RuleFor(q => q.Page)
            .GreaterThan(0)
            .WithErrorCode("ACC-ERR-002")
            .WithMessage("O número da página deve ser maior que zero.");

        RuleFor(q => q.PageSize)
            .GreaterThan(0)
            .WithErrorCode("ACC-ERR-002")
            .WithMessage("O tamanho da página deve ser maior que zero.")
            .LessThanOrEqualTo(MaxPageSize)
            .WithErrorCode("ACC-ERR-002")
            .WithMessage($"O tamanho da página não pode exceder {MaxPageSize}.");
    }
}

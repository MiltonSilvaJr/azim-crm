namespace TenantAdministration.Application.Exceptions;

/// <summary>
/// Representa um erro de validação com código canônico do catálogo TA-ERR.
/// </summary>
/// <param name="PropertyName">Nome da propriedade que falhou.</param>
/// <param name="ErrorMessage">Mensagem de erro.</param>
/// <param name="ErrorCode">Código canônico do catálogo TA-ERR (ex.: <c>TA-ERR-001</c>).</param>
public sealed record ValidationError(string PropertyName, string ErrorMessage, string? ErrorCode);

/// <summary>
/// Exceção lançada pelo <c>ValidationBehavior</c> quando a validação FluentValidation falha.
/// Preserva o código de erro canônico (TA-ERR) de cada falha de validação.
/// Mapeada para HTTP 400 Bad Request ou 422 Unprocessable Entity na camada de API.
/// </summary>
public sealed class ValidationException : Exception
{
    /// <summary>Erros de validação indexados por nome de campo (propriedade).</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <summary>
    /// Lista de erros com código canônico preservado de FluentValidation.
    /// Permite que a camada de Api retorne o <c>errorCode</c> correto no ProblemDetails.
    /// </summary>
    public IReadOnlyList<ValidationError> ValidationErrors { get; }

    /// <summary>Cria uma <see cref="ValidationException"/> com os erros fornecidos.</summary>
    public ValidationException(IDictionary<string, string[]> errors)
        : base("Um ou mais erros de validação ocorreram.")
    {
        Errors = errors.AsReadOnly();
        ValidationErrors = [];
    }

    /// <summary>
    /// Cria uma <see cref="ValidationException"/> preservando os códigos de erro de FluentValidation.
    /// </summary>
    public ValidationException(IEnumerable<ValidationError> validationErrors)
        : base("Um ou mais erros de validação ocorreram.")
    {
        var errors = validationErrors.ToList();
        ValidationErrors = errors.AsReadOnly();
        Errors = errors
            .GroupBy(e => e.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray(),
                StringComparer.Ordinal)
            .AsReadOnly();
    }
}

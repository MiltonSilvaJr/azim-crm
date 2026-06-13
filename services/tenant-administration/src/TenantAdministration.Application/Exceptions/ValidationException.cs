namespace TenantAdministration.Application.Exceptions;

/// <summary>
/// Exceção lançada pelo <c>ValidationBehavior</c> quando a validação FluentValidation falha.
/// Contém a lista de erros de validação indexados por campo.
/// Mapeada para HTTP 400 Bad Request ou 422 Unprocessable Entity na camada de API.
/// </summary>
public sealed class ValidationException : Exception
{
    /// <summary>Erros de validação indexados por nome de campo (propriedade).</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <summary>Cria uma <see cref="ValidationException"/> com os erros fornecidos.</summary>
    public ValidationException(IDictionary<string, string[]> errors)
        : base("Um ou mais erros de validação ocorreram.")
    {
        Errors = errors.AsReadOnly();
    }
}

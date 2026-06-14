namespace OpportunityPipeline.Application.Common;

/// <summary>
/// Exceção lançada pelo ValidationBehavior quando FluentValidation detecta erros.
/// Mapeia a HTTP 422 com código OP-ERR-* no middleware de erros.
/// Mapeia: design §5.5, Req 1..16.
/// </summary>
public sealed class ValidationException : Exception
{
    /// <summary>Erros de validação por campo.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <summary>Código de erro do catálogo OP-ERR-* (quando aplicável).</summary>
    public string? ErrorCode { get; }

    /// <param name="errors">Dicionário campo → mensagens de erro.</param>
    /// <param name="errorCode">Código OP-ERR-* opcional.</param>
    public ValidationException(
        IReadOnlyDictionary<string, string[]> errors,
        string? errorCode = null)
        : base("Um ou mais erros de validação ocorreram.")
    {
        Errors = errors;
        ErrorCode = errorCode;
    }

    /// <param name="field">Campo com erro.</param>
    /// <param name="message">Mensagem de erro.</param>
    /// <param name="errorCode">Código OP-ERR-* opcional.</param>
    public ValidationException(string field, string message, string? errorCode = null)
        : this(new Dictionary<string, string[]> { [field] = [message] }, errorCode)
    {
    }
}

namespace TenantAdministration.Application.Exceptions;

/// <summary>
/// Exceção lançada quando uma regra de domínio ou aplicação falha com um código
/// de erro canônico do catálogo TA-ERR-* (design.md §12).
/// Mapeada para HTTP 4xx na camada de API conforme o código de erro.
/// </summary>
public sealed class DomainValidationException : Exception
{
    /// <summary>Código de erro canônico (ex.: <c>TA-ERR-002</c>).</summary>
    public string ErrorCode { get; }

    /// <summary>Cria uma <see cref="DomainValidationException"/> com código e mensagem.</summary>
    public DomainValidationException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}

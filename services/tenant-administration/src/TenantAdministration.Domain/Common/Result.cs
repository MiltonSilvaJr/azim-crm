namespace TenantAdministration.Domain.Common;

/// <summary>
/// Representa o resultado de uma operação que pode falhar com uma mensagem de erro.
/// </summary>
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T value)
    {
        IsSuccess = true;
        _value = value;
        ErrorCode = null;
        ErrorMessage = null;
    }

    private Result(string errorCode, string errorMessage)
    {
        IsSuccess = false;
        _value = default;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>Indica se a operação foi bem-sucedida.</summary>
    public bool IsSuccess { get; }

    /// <summary>Indica se a operação falhou.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Código de erro canônico (ex.: TA-ERR-005). Nulo quando bem-sucedido.</summary>
    public string? ErrorCode { get; }

    /// <summary>Mensagem de erro legível. Nula quando bem-sucedida.</summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Valor resultante da operação. Lança exceção se chamado em caso de falha.
    /// </summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Não é possível acessar o valor de um resultado de falha.");

    /// <summary>Cria um resultado de sucesso com o valor fornecido.</summary>
    public static Result<T> Success(T value) => new(value);

    /// <summary>Cria um resultado de falha com código e mensagem de erro.</summary>
    public static Result<T> Failure(string errorCode, string errorMessage) => new(errorCode, errorMessage);
}

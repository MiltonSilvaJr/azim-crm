namespace Organization.Domain.Exceptions;

/// <summary>
/// Exceção base para violações de invariantes de domínio.
/// Usada pelos agregados para sinalizar tentativas de operação inválida.
/// </summary>
public class DomainException : Exception
{
    /// <summary>Código de erro do catálogo (ex.: ORG-ERR-015).</summary>
    public string ErrorCode { get; }

    /// <summary>Inicializa a exceção de domínio com código e mensagem.</summary>
    public DomainException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>Inicializa a exceção de domínio com código, mensagem e causa.</summary>
    public DomainException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

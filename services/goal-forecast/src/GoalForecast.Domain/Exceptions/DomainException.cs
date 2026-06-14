namespace GoalForecast.Domain.Exceptions;

/// <summary>
/// Exceção base para violações de invariantes de domínio do módulo goal-forecast.
/// Subtipos identificam o código de erro canônico (catálogo §12 do design).
/// </summary>
public class DomainException : Exception
{
    /// <summary>
    /// Código de erro canônico do catálogo (ex.: GF-ERR-001).
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Inicializa uma nova instância de <see cref="DomainException"/>.
    /// </summary>
    /// <param name="errorCode">Código canônico do catálogo de erros (design §12).</param>
    /// <param name="message">Mensagem descritiva da violação.</param>
    public DomainException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Inicializa uma nova instância de <see cref="DomainException"/> com causa raiz.
    /// </summary>
    /// <param name="errorCode">Código canônico do catálogo de erros (design §12).</param>
    /// <param name="message">Mensagem descritiva da violação.</param>
    /// <param name="innerException">Exceção que originou esta violação.</param>
    public DomainException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

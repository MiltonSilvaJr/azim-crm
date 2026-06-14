namespace GoalForecast.Application.Common;

/// <summary>
/// Exceção de aplicação para erros de negócio não cobertos por exceções de domínio.
/// Carrega o código de erro canônico do catálogo (design §12).
/// Mapeia: catálogo de erros GF-ERR-* (design §12).
/// </summary>
public sealed class ApplicationException : Exception
{
    /// <summary>Código de erro canônico (ex.: GF-ERR-004, GF-ERR-006).</summary>
    public string ErrorCode { get; }

    /// <summary>Status HTTP sugerido para conversão no controller.</summary>
    public int SuggestedHttpStatus { get; }

    /// <summary>
    /// Cria uma exceção de aplicação com código de erro e mensagem.
    /// </summary>
    public ApplicationException(string errorCode, string message, int suggestedHttpStatus = 400)
        : base(message)
    {
        ErrorCode = errorCode;
        SuggestedHttpStatus = suggestedHttpStatus;
    }
}

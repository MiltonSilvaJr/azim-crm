namespace Reporting.Application.Exceptions;

/// <summary>
/// Lançada pelo <c>AuthorizationBehavior</c> quando o papel não tem escopo de acesso
/// (incluindo negação dura para PlatformOperator).
/// Mapeia: design §5.4, RNF 5, DD-006, REPORT-ERR-005.
/// </summary>
public sealed class AccessDeniedException : Exception
{
    /// <summary>Código de erro canônico.</summary>
    public const string ErrorCode = "REPORT-ERR-005";

    /// <summary>Inicializa com mensagem padrão.</summary>
    public AccessDeniedException()
        : base("Acesso negado ao relatório. (REPORT-ERR-005)") { }

    /// <summary>Inicializa com mensagem customizada.</summary>
    public AccessDeniedException(string message) : base(message) { }

    /// <summary>Inicializa com mensagem e exceção interna.</summary>
    public AccessDeniedException(string message, Exception innerException)
        : base(message, innerException) { }
}

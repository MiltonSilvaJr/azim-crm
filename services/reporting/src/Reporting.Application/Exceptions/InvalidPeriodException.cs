namespace Reporting.Application.Exceptions;

/// <summary>
/// Lançada quando o período informado é inválido (ex.: <c>from &gt; to</c>).
/// Mapeada para 400 Bad Request com código REPORT-ERR-001.
///
/// Mapeia: design §12, TASK-21, REPORT-ERR-001.
/// </summary>
public sealed class InvalidPeriodException : Exception
{
    /// <summary>Código de erro canônico.</summary>
    public const string ErrorCode = "REPORT-ERR-001";

    /// <summary>Inicializa com mensagem padrão.</summary>
    public InvalidPeriodException()
        : base("Período inválido. A data 'from' deve ser anterior ou igual a 'to'.") { }

    /// <summary>Inicializa com mensagem customizada.</summary>
    public InvalidPeriodException(string message) : base(message) { }

    /// <summary>Inicializa com mensagem e exceção interna.</summary>
    public InvalidPeriodException(string message, Exception innerException)
        : base(message, innerException) { }
}

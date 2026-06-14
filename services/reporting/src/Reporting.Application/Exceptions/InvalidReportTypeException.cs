namespace Reporting.Application.Exceptions;

/// <summary>
/// Lançada quando o tipo de relatório informado para o export não é reconhecido.
/// Mapeada para 400 Bad Request com código REPORT-ERR-003.
///
/// Mapeia: design §12, TASK-21, REPORT-ERR-003.
/// </summary>
public sealed class InvalidReportTypeException : Exception
{
    /// <summary>Código de erro canônico.</summary>
    public const string ErrorCode = "REPORT-ERR-003";

    /// <summary>Inicializa com o tipo inválido informado.</summary>
    /// <param name="reportType">String de tipo inválida (não exposta no response — apenas para logging interno).</param>
    public InvalidReportTypeException(string reportType)
        : base($"Tipo de relatório não reconhecido: '{reportType}'. Use funnel, forecast, ranking, channel ou commissions.") { }
}

namespace AuditLog.Application.Errors;

/// <summary>
/// Exceção base para erros de aplicação do módulo de auditoria.
/// Transporta o código de erro do catálogo <see cref="AuditErrorCodes"/> para mapeamento HTTP.
/// </summary>
public abstract class AuditApplicationException : Exception
{
    /// <summary>Código de erro do catálogo (<see cref="AuditErrorCodes"/>).</summary>
    public string ErrorCode { get; }

    /// <summary>Inicializa a exceção com código e mensagem.</summary>
    protected AuditApplicationException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Exceção lançada quando o acesso à trilha de auditoria é negado por papel ou contexto de tenant ausente.
/// Mapeia para HTTP 403 (AUD-ERR-002 ou AUD-ERR-008).
/// </summary>
public sealed class AuditAuthorizationException : AuditApplicationException
{
    /// <summary>
    /// Inicializa a exceção de autorização.
    /// </summary>
    /// <param name="errorCode">Código de erro (<see cref="AuditErrorCodes.AccessDenied"/> ou <see cref="AuditErrorCodes.TenantContextMissing"/>).</param>
    /// <param name="message">Mensagem descritiva (sem PII, sem detalhes internos).</param>
    public AuditAuthorizationException(string errorCode, string message)
        : base(errorCode, message)
    {
    }
}

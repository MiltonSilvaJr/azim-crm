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

/// <summary>
/// Exceção lançada quando um parâmetro de consulta é inválido (AUD-ERR-001 ou AUD-ERR-006).
/// Mapeia para HTTP 400 Bad Request.
/// </summary>
public sealed class AuditValidationException : AuditApplicationException
{
    /// <summary>
    /// Inicializa a exceção de validação.
    /// </summary>
    /// <param name="errorCode">Código de erro (ex.: <see cref="AuditErrorCodes.InvalidFilter"/>, <see cref="AuditErrorCodes.InvalidDateRange"/>).</param>
    /// <param name="message">Mensagem descritiva (sem PII).</param>
    public AuditValidationException(string errorCode, string message)
        : base(errorCode, message)
    {
    }
}

/// <summary>
/// Exceção lançada quando nenhum registro de auditoria é encontrado para a entidade consultada.
/// Mapeia para HTTP 404 Not Found (AUD-ERR-004).
/// <para>
/// Uso opcional — o padrão do módulo é retornar 200 com lista vazia (design §12).
/// </para>
/// </summary>
public sealed class AuditNotFoundException : AuditApplicationException
{
    /// <summary>
    /// Inicializa a exceção de recurso não encontrado.
    /// </summary>
    /// <param name="errorCode">Código de erro (<see cref="AuditErrorCodes.NotFound"/>).</param>
    /// <param name="message">Mensagem descritiva (sem PII — anti-enumeração, design §12).</param>
    public AuditNotFoundException(string errorCode, string message)
        : base(errorCode, message)
    {
    }
}

/// <summary>
/// Exceção lançada quando o INSERT em <c>audit_logs</c> falha (AUD-ERR-005, DD-001).
/// Mapeia para HTTP 500 Internal Server Error.
/// <para>
/// A mensagem nunca expõe conteúdo de <c>delta_json</c> nem valores de PII (RNF-002.3).
/// </para>
/// </summary>
public sealed class AuditPersistenceException : AuditApplicationException
{
    /// <summary>
    /// Inicializa a exceção de falha de persistência.
    /// </summary>
    /// <param name="errorCode">Código de erro (<see cref="AuditErrorCodes.PersistenceFailure"/>).</param>
    /// <param name="message">Mensagem descritiva (sem PII nem delta).</param>
    public AuditPersistenceException(string errorCode, string message)
        : base(errorCode, message)
    {
    }

    /// <summary>
    /// Inicializa a exceção com inner exception para rastreabilidade interna (sem exposição externa).
    /// </summary>
    public AuditPersistenceException(string errorCode, string message, Exception innerException)
        : base(errorCode, message)
    {
        _ = innerException; // capturado via logging interno, não repassado
    }
}

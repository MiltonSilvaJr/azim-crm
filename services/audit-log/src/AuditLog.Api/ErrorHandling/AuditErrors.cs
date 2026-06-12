using AuditLog.Application.Errors;

namespace AuditLog.Api.ErrorHandling;

/// <summary>
/// Dicionário de erros do módulo de auditoria (design §12).
/// Mapeia código de erro → (statusHTTP, título do ProblemDetails).
/// <para>
/// AUD-ERR-002 e AUD-ERR-004 compartilham o mesmo título intencionalmente
/// para prevenir enumeração de recursos (REQ-005.3, design §12).
/// </para>
/// </summary>
public static class AuditErrors
{
    /// <summary>Mensagem uniforme usada por AUD-ERR-002 e AUD-ERR-004 (anti-enumeração).</summary>
    private const string UniformAccessTitle = "Acesso negado ou recurso não encontrado.";

    /// <summary>
    /// Mapa de código de erro → (HTTP status code, título do ProblemDetails).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (int StatusCode, string Title)> Catalog =
        new Dictionary<string, (int, string)>(StringComparer.OrdinalIgnoreCase)
        {
            [AuditErrorCodes.InvalidFilter]        = (400, "Filtro de consulta inválido."),
            [AuditErrorCodes.AccessDenied]         = (403, UniformAccessTitle),
            [AuditErrorCodes.AuthenticationRequired] = (401, "Autenticação necessária."),
            [AuditErrorCodes.NotFound]             = (404, UniformAccessTitle),   // mesmo título → anti-enumeração
            [AuditErrorCodes.PersistenceFailure]   = (500, "Falha ao processar a operação."),
            [AuditErrorCodes.InvalidDateRange]     = (400, "Período de consulta inválido."),
            [AuditErrorCodes.OperationNotAllowed]  = (405, "Operação não permitida na trilha."),
            [AuditErrorCodes.TenantContextMissing] = (403, "Contexto de autenticação inválido.")
        };
}

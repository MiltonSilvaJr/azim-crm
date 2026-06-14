namespace Reporting.Application.Exceptions;

/// <summary>
/// Lançada pelo <c>TenantContextBehavior</c> quando <c>tenant_id</c> está ausente ou inválido.
/// Falha-fechada: sem tenant válido, a query é abortada antes de tocar o banco.
/// Mapeia: design §5.4, ADR-0001, Req 8, REPORT-ERR-409.
/// </summary>
public sealed class TenantNotResolvedException : Exception
{
    /// <summary>Código de erro canônico.</summary>
    public const string ErrorCode = "REPORT-ERR-409";

    /// <summary>Inicializa com mensagem padrão.</summary>
    public TenantNotResolvedException()
        : base("Tenant não resolvido. Reautentique e tente novamente. (REPORT-ERR-409)") { }

    /// <summary>Inicializa com mensagem customizada.</summary>
    public TenantNotResolvedException(string message) : base(message) { }

    /// <summary>Inicializa com mensagem e exceção interna.</summary>
    public TenantNotResolvedException(string message, Exception innerException)
        : base(message, innerException) { }
}

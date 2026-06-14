namespace Reporting.Application.Exceptions;

/// <summary>
/// Lançada quando um <c>buId</c> fornecido na query está fora do escopo RBAC do usuário.
/// Mapeada para 404 Not Found com código REPORT-ERR-004 (anti-enumeração).
///
/// A resposta 404 genérica impede que o chamador enumere BUs de outros tenants
/// (design §10, §12, RNF 4.3).
///
/// Mapeia: design §10, §12, TASK-21, REPORT-ERR-004.
/// </summary>
public sealed class BuIdOutOfScopeException : Exception
{
    /// <summary>Código de erro canônico.</summary>
    public const string ErrorCode = "REPORT-ERR-004";

    /// <summary>Inicializa com mensagem padrão.</summary>
    public BuIdOutOfScopeException()
        : base("Recurso não encontrado no seu escopo.") { }

    /// <summary>Inicializa com mensagem customizada.</summary>
    public BuIdOutOfScopeException(string message) : base(message) { }
}

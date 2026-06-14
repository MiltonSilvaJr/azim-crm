namespace GoalForecast.Domain.Authorization;

/// <summary>
/// Resultado de uma decisão de autorização do domínio.
/// Quando negado, contém o código de erro canônico GF-ERR-006.
/// Não revela existência de recursos fora do escopo (RNF-2.3).
///
/// Mapeia: Req 12, RNF-2.3, design §10, catálogo de erros §12, TASK-07.
/// </summary>
public sealed record AuthorizationResult
{
    /// <summary>Indica se a operação foi autorizada.</summary>
    public bool IsAllowed { get; }

    /// <summary>
    /// Código de erro canônico quando negado (GF-ERR-006).
    /// Nulo quando autorizado.
    /// </summary>
    public string? ErrorCode { get; }

    private AuthorizationResult(bool isAllowed, string? errorCode)
    {
        IsAllowed = isAllowed;
        ErrorCode = errorCode;
    }

    /// <summary>Instância estática para resultado permitido.</summary>
    public static readonly AuthorizationResult Allowed = new(true, null);

    /// <summary>
    /// Cria um resultado de negação com código GF-ERR-006.
    /// Não revela se o recurso existe ou não (RNF-2.3).
    /// </summary>
    public static AuthorizationResult Denied() => new(false, "GF-ERR-006");
}

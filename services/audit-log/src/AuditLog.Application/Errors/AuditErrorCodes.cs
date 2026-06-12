namespace AuditLog.Application.Errors;

/// <summary>
/// Catálogo de códigos de erro do módulo de auditoria (design §12).
/// Todos os erros de aplicação referenciam estes códigos para rastreabilidade.
/// </summary>
public static class AuditErrorCodes
{
    /// <summary>Filtro de consulta inválido (400). Parâmetro mal formado.</summary>
    public const string InvalidFilter = "AUD-ERR-001";

    /// <summary>Acesso negado à trilha de auditoria (403). Papel sem permissão ou fora do escopo de BU.</summary>
    public const string AccessDenied = "AUD-ERR-002";

    /// <summary>Autenticação necessária (401). Requisição sem JWT válido.</summary>
    public const string AuthenticationRequired = "AUD-ERR-003";

    /// <summary>Nenhum registro de auditoria encontrado (404). Opcional — padrão é 200 com lista vazia.</summary>
    public const string NotFound = "AUD-ERR-004";

    /// <summary>Falha ao persistir registro de auditoria (500). INSERT falhou; transação revertida (fail-closed, DD-001).</summary>
    public const string PersistenceFailure = "AUD-ERR-005";

    /// <summary>Período de consulta inválido (400). <c>from</c> &gt; <c>to</c> ou intervalo fora dos limites.</summary>
    public const string InvalidDateRange = "AUD-ERR-006";

    /// <summary>Operação não permitida na trilha (405). Tentativa de escrita/edição/exclusão via API.</summary>
    public const string OperationNotAllowed = "AUD-ERR-007";

    /// <summary>Contexto de tenant ausente (403). Consulta sem <c>tenant_id</c> no contexto autenticado (DD-007).</summary>
    public const string TenantContextMissing = "AUD-ERR-008";
}

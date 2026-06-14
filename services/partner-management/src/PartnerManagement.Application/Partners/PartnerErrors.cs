namespace PartnerManagement.Application.Partners;

/// <summary>
/// Catálogo de códigos de erro do módulo partner-management.
/// Mapeia: design §12, Req 1..12.
/// </summary>
public static class PartnerErrors
{
    /// <summary>PM-ERR-001 — Nome do parceiro é obrigatório.</summary>
    public const string NameRequired = "PM-ERR-001";

    /// <summary>PM-ERR-002 — Papel de parceiro inválido.</summary>
    public const string InvalidRole = "PM-ERR-002";

    /// <summary>PM-ERR-003 — Percentual fora do intervalo permitido.</summary>
    public const string PercentageOutOfRange = "PM-ERR-003";

    /// <summary>PM-ERR-004 — E-mail de contato inválido.</summary>
    public const string InvalidContactEmail = "PM-ERR-004";

    /// <summary>PM-ERR-005 — Parceiro inativo não pode ser vinculado.</summary>
    public const string PartnerInactive = "PM-ERR-005";

    /// <summary>PM-ERR-007 — Parceiro não encontrado (ou fora do tenant).</summary>
    public const string PartnerNotFound = "PM-ERR-007";

    /// <summary>PM-ERR-008 — Acesso negado (papel insuficiente).</summary>
    public const string AccessDenied = "PM-ERR-008";

    /// <summary>PM-ERR-009 — Parâmetros de listagem inválidos.</summary>
    public const string InvalidListParameters = "PM-ERR-009";

    /// <summary>PM-ERR-010 — Requisição duplicada (Idempotency-Key reutilizada com payload divergente).</summary>
    public const string DuplicateRequest = "PM-ERR-010";

    /// <summary>PM-ERR-011 — Período de comissão inválido.</summary>
    public const string InvalidCommissionPeriod = "PM-ERR-011";
}

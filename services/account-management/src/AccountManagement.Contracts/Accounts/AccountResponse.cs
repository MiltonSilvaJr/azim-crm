namespace AccountManagement.Contracts.Accounts;

/// <summary>
/// Resposta de detalhe de uma conta.
///
/// Sem <c>bu_id</c> — a conta é compartilhada por todo o tenant (Req 2.3).
/// Sem PII de contato — para listar contatos, usar o endpoint de contatos.
///
/// Mapeia: design §8, Req 2.3, Req 3.
/// </summary>
public sealed record AccountResponse(
    /// <summary>Identificador único da conta.</summary>
    Guid Id,
    /// <summary>Tenant proprietário.</summary>
    Guid TenantId,
    /// <summary>Nome da conta.</summary>
    string Name,
    /// <summary>Forma normalizada do nome (base de dedupe — DD-005).</summary>
    string NormalizedName,
    /// <summary>Website da conta (opcional).</summary>
    string? Website,
    /// <summary>Observações da conta (opcional).</summary>
    string? Notes,
    /// <summary>Carimbo de criação (ISO 8601, UTC).</summary>
    DateTimeOffset CreatedAt,
    /// <summary>Carimbo de última atualização (ISO 8601, UTC).</summary>
    DateTimeOffset UpdatedAt);

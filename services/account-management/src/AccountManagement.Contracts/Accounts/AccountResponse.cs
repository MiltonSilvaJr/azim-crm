namespace AccountManagement.Contracts.Accounts;

/// <summary>
/// Resposta de detalhe de uma conta.
///
/// Inclui <c>bu_id</c> — a conta pertence a exatamente uma Business Unit (ADR-0009, Req 2.3 revisado).
/// Sem PII de contato — para listar contatos, usar o endpoint de contatos.
///
/// Mapeia: design §8, ADR-0009, Req 2.3 (revisado), Req 3.
/// </summary>
public sealed record AccountResponse(
    /// <summary>Identificador único da conta.</summary>
    Guid Id,
    /// <summary>Tenant proprietário.</summary>
    Guid TenantId,
    /// <summary>Business Unit dona da conta (ADR-0009).</summary>
    Guid BuId,
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

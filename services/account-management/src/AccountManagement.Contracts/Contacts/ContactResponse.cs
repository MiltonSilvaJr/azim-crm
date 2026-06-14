namespace AccountManagement.Contracts.Contacts;

/// <summary>
/// Resposta de contato.
///
/// Campos de PII (<see cref="Name"/>, <see cref="Email"/>, <see cref="Phone"/>) são
/// retornados apenas quando o usuário tem papel mínimo Vendedor na BU (Req 9.1, RNF 6).
/// Quando acesso PII é negado, o endpoint retorna 403 ACC-ERR-008 (anti-enumeração).
///
/// Após anonimização (<see cref="IsAnonymized"/> = true), <see cref="Name"/> retorna
/// o marcador de anonimização e PII é irrecuperável (DD-001, Req 7.5, PBT-03).
///
/// Mapeia: design §8, Req 5, Req 7, Req 9, RNF 6, DD-001, PBT-03.
/// </summary>
public sealed record ContactResponse(
    /// <summary>Identificador do contato. Preservado mesmo após anonimização (Req 7.3).</summary>
    Guid Id,
    /// <summary>Identificador da conta à qual o contato pertence.</summary>
    Guid AccountId,
    /// <summary>Nome do contato (PII — substituído por marcador quando anonimizado).</summary>
    string Name,
    /// <summary>E-mail do contato (PII — null quando anonimizado).</summary>
    string? Email,
    /// <summary>Telefone do contato (PII — null quando anonimizado).</summary>
    string? Phone,
    /// <summary>Cargo do contato (não é PII sensível — null quando limpo).</summary>
    string? Role,
    /// <summary>Estado de privacidade: false=ativo, true=anonimizado.</summary>
    bool IsAnonymized,
    /// <summary>Carimbo de criação (ISO 8601, UTC).</summary>
    DateTimeOffset CreatedAt,
    /// <summary>Carimbo de última atualização (ISO 8601, UTC).</summary>
    DateTimeOffset UpdatedAt);

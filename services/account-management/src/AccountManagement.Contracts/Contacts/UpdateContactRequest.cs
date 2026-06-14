namespace AccountManagement.Contracts.Contacts;

/// <summary>
/// Payload de requisição para atualizar um contato existente.
///
/// Campos de PII: <see cref="Name"/>, <see cref="Email"/>, <see cref="Phone"/>.
/// Requer papel mínimo Vendedor na BU (Req 9.1, RNF 6).
///
/// Mapeia: design §8, Req 5, ACC-ERR-004, ACC-ERR-005, ACC-ERR-006, ACC-ERR-008.
/// </summary>
public sealed record UpdateContactRequest(
    /// <summary>Novo nome do contato (PII — obrigatório; ACC-ERR-005 se vazio).</summary>
    string Name,
    /// <summary>Novo e-mail (PII — opcional; ACC-ERR-004 se formato inválido).</summary>
    string? Email,
    /// <summary>Novo telefone (PII — opcional).</summary>
    string? Phone,
    /// <summary>Novo cargo (não é PII sensível — opcional).</summary>
    string? Role);

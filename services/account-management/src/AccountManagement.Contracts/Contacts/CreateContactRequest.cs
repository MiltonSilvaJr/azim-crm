namespace AccountManagement.Contracts.Contacts;

/// <summary>
/// Payload de requisição para criar um contato vinculado a uma conta.
///
/// Campos de PII: <see cref="Name"/>, <see cref="Email"/>, <see cref="Phone"/>.
/// Requer papel mínimo Vendedor na BU (Req 9.1, RNF 6).
///
/// Mapeia: design §8, Req 5, ACC-ERR-004, ACC-ERR-005, ACC-ERR-008.
/// </summary>
public sealed record CreateContactRequest(
    /// <summary>Nome do contato (PII — obrigatório; ACC-ERR-005 se vazio).</summary>
    string Name,
    /// <summary>E-mail do contato (PII — opcional; ACC-ERR-004 se formato inválido).</summary>
    string? Email,
    /// <summary>Telefone do contato (PII — opcional).</summary>
    string? Phone,
    /// <summary>Cargo do contato (não é PII sensível — opcional).</summary>
    string? Role);

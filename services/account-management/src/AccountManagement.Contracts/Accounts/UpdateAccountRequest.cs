namespace AccountManagement.Contracts.Accounts;

/// <summary>
/// Payload de requisição para atualizar uma conta existente.
///
/// Mapeia: design §8, Req 4, ACC-ERR-001, ACC-ERR-003.
/// </summary>
public sealed record UpdateAccountRequest(
    /// <summary>Novo nome da conta (obrigatório — ACC-ERR-001 se vazio).</summary>
    string Name,
    /// <summary>Novo website (opcional).</summary>
    string? Website,
    /// <summary>Novas observações (opcional).</summary>
    string? Notes);

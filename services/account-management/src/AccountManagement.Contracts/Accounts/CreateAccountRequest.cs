namespace AccountManagement.Contracts.Accounts;

/// <summary>
/// Payload de requisição para criar uma nova conta.
///
/// A flag <see cref="ConfirmCreateDespiteSimilar"/> permite confirmar a criação
/// mesmo quando candidatos similares existem (dedupe não-bloqueante — DD-006).
///
/// Mapeia: design §8, Req 1, ACC-ERR-001, DD-006.
/// </summary>
public sealed record CreateAccountRequest(
    /// <summary>Nome da conta (obrigatório — ACC-ERR-001 se vazio).</summary>
    string Name,
    /// <summary>Website da conta (opcional).</summary>
    string? Website,
    /// <summary>Observações opcionais.</summary>
    string? Notes,
    /// <summary>
    /// <c>true</c> indica que o usuário reconheceu contas similares e confirma a criação.
    /// Não impede o POST — apenas registra a intenção (DD-006).
    /// </summary>
    bool ConfirmCreateDespiteSimilar = false);

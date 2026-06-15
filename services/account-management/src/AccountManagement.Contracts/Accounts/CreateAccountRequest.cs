namespace AccountManagement.Contracts.Accounts;

/// <summary>
/// Payload de requisição para criar uma nova conta.
///
/// A flag <see cref="ConfirmCreateDespiteSimilar"/> permite confirmar a criação
/// mesmo quando candidatos similares existem (dedupe não-bloqueante — DD-006).
///
/// O campo <see cref="BuId"/> determina a Business Unit dona da conta (ADR-0009):
/// - Quando o usuário pertence a exatamente 1 BU, pode ser omitido (resolvido automaticamente).
/// - Quando o usuário pertence a múltiplas BUs, torna-se obrigatório (ACC-ERR-010).
/// - O bu_id informado deve estar no escopo de BUs do usuário (ACC-ERR-011).
///
/// Mapeia: design §8, Req 1, ACC-ERR-001, ACC-ERR-010, ACC-ERR-011, DD-006, ADR-0009.
/// </summary>
public sealed record CreateAccountRequest(
    /// <summary>Nome da conta (obrigatório — ACC-ERR-001 se vazio).</summary>
    string Name,
    /// <summary>Website da conta (opcional).</summary>
    string? Website,
    /// <summary>Observações opcionais.</summary>
    string? Notes,
    /// <summary>
    /// Business Unit dona da conta (opcional quando usuário tem 1 BU; obrigatório para múltiplas — ACC-ERR-010).
    /// O bu_id deve pertencer ao escopo de BUs do usuário autenticado (ACC-ERR-011).
    /// </summary>
    Guid? BuId = null,
    /// <summary>
    /// <c>true</c> indica que o usuário reconheceu contas similares e confirma a criação.
    /// Não impede o POST — apenas registra a intenção (DD-006).
    /// </summary>
    bool ConfirmCreateDespiteSimilar = false);

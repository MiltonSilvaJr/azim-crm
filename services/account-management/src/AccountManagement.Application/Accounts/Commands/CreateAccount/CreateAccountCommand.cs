using MediatR;

namespace AccountManagement.Application.Accounts.Commands.CreateAccount;

/// <summary>
/// Command para criar uma nova conta no tenant.
///
/// A flag <see cref="ConfirmCreateDespiteSimilar"/> indica que o usuário reconheceu
/// a existência de contas similares e confirma a criação mesmo assim (dedupe não-bloqueante — DD-006).
/// O handler cria a conta independentemente da flag.
///
/// O campo <see cref="BuId"/> é opcional no command: quando nulo, o handler resolve
/// automaticamente a BU se o usuário pertence a exatamente uma BU; quando o usuário
/// pertence a múltiplas BUs, <see cref="BuId"/> torna-se obrigatório (ADR-0009).
///
/// Mapeia: design §5.1, Req 1, DD-006, ACC-ERR-001, ACC-ERR-010, ACC-ERR-011, ADR-0009.
/// </summary>
/// <param name="TenantId">Tenant proprietário da conta.</param>
/// <param name="Name">Nome da conta (obrigatório — ACC-ERR-001 se vazio).</param>
/// <param name="Website">Website da conta (opcional).</param>
/// <param name="Notes">Observações opcionais.</param>
/// <param name="BuId">
/// Business Unit da conta (opcional quando usuário tem 1 BU; obrigatório quando tem várias — ACC-ERR-010).
/// </param>
/// <param name="ConfirmCreateDespiteSimilar">
/// <c>true</c> quando o usuário confirmou a criação apesar de contas similares.
/// Não altera o comportamento do handler (criação sempre ocorre — DD-006).
/// </param>
public sealed record CreateAccountCommand(
    Guid TenantId,
    string Name,
    string? Website,
    string? Notes,
    Guid? BuId,
    bool ConfirmCreateDespiteSimilar) : IRequest<Guid>;

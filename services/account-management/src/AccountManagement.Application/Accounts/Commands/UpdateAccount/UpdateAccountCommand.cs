using MediatR;

namespace AccountManagement.Application.Accounts.Commands.UpdateAccount;

/// <summary>
/// Command para atualizar os dados de uma conta existente.
///
/// O handler recalcula o <c>NormalizedName</c> a partir do novo nome via domínio (I2).
///
/// Mapeia: design §5.1, Req 4, ACC-ERR-001, ACC-ERR-003.
/// </summary>
/// <param name="AccountId">Identificador da conta a atualizar.</param>
/// <param name="Name">Novo nome da conta (obrigatório).</param>
/// <param name="Website">Novo website (opcional).</param>
/// <param name="Notes">Novas observações (opcional).</param>
public sealed record UpdateAccountCommand(
    Guid AccountId,
    string Name,
    string? Website,
    string? Notes) : IRequest;

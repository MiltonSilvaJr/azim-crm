using MediatR;

namespace AccountManagement.Application.Accounts.Queries.GetAccount360;

/// <summary>
/// Query para compor a visão 360° de uma conta.
///
/// Agrega dados próprios da conta (dados cadastrais + contatos) com dados externos
/// via <see cref="IOpportunityReadPort"/> e <see cref="IActivityReadPort"/> (DD-004, Req 6).
///
/// Oportunidades são filtradas pelo escopo de BUs do usuário autenticado (Req 6.2/6.3, PBT-05).
/// Se uma porta downstream falhar, a seção correspondente é marcada como indisponível
/// sem derrubar a 360° inteira — degradação parcial (design §5.2, §15, DD-004).
///
/// Mapeia: design §5.2, Req 6, PBT-05, DD-004, ACC-ERR-003.
/// </summary>
/// <param name="AccountId">Identificador da conta a compor.</param>
/// <param name="AuthorizedBuIds">
/// Conjunto de BUs que o usuário autenticado pode ver.
/// Passado explicitamente para garantir filtragem correta (PBT-05).
/// </param>
public sealed record GetAccount360Query(
    Guid AccountId,
    IReadOnlySet<Guid> AuthorizedBuIds) : IRequest<Account360View>;

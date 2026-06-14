using AccountManagement.Domain.Accounts;
using MediatR;

namespace AccountManagement.Application.Accounts.Queries.GetAccountById;

/// <summary>
/// Query para obter o detalhe de uma conta por identificador.
///
/// Retorna a conta sem a visão 360° (para isso, usar <see cref="GetAccount360.GetAccount360Query"/>).
/// Lança <see cref="Exceptions.AccountNotFoundException"/> (ACC-ERR-003) quando a conta
/// não existe ou não pertence ao tenant do contexto (anti-enumeração — Req 9, PBT-04).
///
/// Mapeia: design §5.2, Req 3, ACC-ERR-003.
/// </summary>
/// <param name="AccountId">Identificador da conta.</param>
public sealed record GetAccountByIdQuery(Guid AccountId) : IRequest<Account>;

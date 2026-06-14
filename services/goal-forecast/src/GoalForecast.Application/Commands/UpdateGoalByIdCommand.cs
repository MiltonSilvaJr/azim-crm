using GoalForecast.Application.Behaviors;
using GoalForecast.Application.Common;
using GoalForecast.Domain.Authorization;
using MediatR;

namespace GoalForecast.Application.Commands;

/// <summary>
/// Comando para atualizar o valor_meta de uma meta existente por ID.
/// Usado no endpoint PUT /api/v1/goals/{id} (design §8.2).
///
/// TenantId extraído do principal autenticado — nunca do payload (Req 12.1).
/// Retorna GF-ERR-007 se o id não existir no tenant.
///
/// Mapeia: Req 4, design §8.2, TASK-22.
/// </summary>
public sealed record UpdateGoalByIdCommand : IRequest<GoalDto>, IHasPrincipal
{
    /// <summary>Principal autenticado. TenantId extraído daqui.</summary>
    public required GoalPrincipal Principal { get; init; }

    /// <summary>Identificador UUID da meta a atualizar.</summary>
    public required Guid GoalId { get; init; }

    /// <summary>Novo valor da meta em centavos inteiros não-negativos (RNF 4).</summary>
    public required long ValorMeta { get; init; }
}

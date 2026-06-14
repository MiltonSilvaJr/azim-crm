using GoalForecast.Application.Behaviors;
using GoalForecast.Application.Common;
using GoalForecast.Domain.Authorization;
using GoalForecast.Domain.ValueObjects;
using MediatR;

namespace GoalForecast.Application.Commands;

/// <summary>
/// Comando único de escrita com semântica upsert idempotente por chave natural
/// <c>(tenant, bu, owner, year, month)</c>. TenantId nunca é aceito do payload —
/// sempre derivado do contexto autenticado (Req 12.1, design §5.1).
///
/// Retorna <see cref="GoalDto"/> com indicação se foi criação (Created=true) ou
/// atualização (Created=false).
///
/// Mapeia: Req 1, Req 2, Req 4, PBT-01, DD-002, design §5.1, TASK-09.
/// </summary>
public sealed record CreateOrUpdateGoalCommand : IRequest<CreateOrUpdateGoalResult>, IHasPrincipal
{
    /// <summary>
    /// Principal autenticado que origina o comando.
    /// TenantId extraído daqui, nunca do payload (Req 12.1).
    /// </summary>
    public required GoalPrincipal Principal { get; init; }

    /// <summary>Escopo da meta: BU ou RESPONSAVEL.</summary>
    public required string Scope { get; init; }

    /// <summary>Identificador da unidade de negócio. Obrigatório.</summary>
    public required Guid BuId { get; init; }

    /// <summary>Identificador do responsável. Obrigatório em RESPONSAVEL, nulo em BU.</summary>
    public Guid? OwnerId { get; init; }

    /// <summary>Ano do período. Quatro dígitos.</summary>
    public required int Year { get; init; }

    /// <summary>Mês do período. 1..12.</summary>
    public required int Month { get; init; }

    /// <summary>Valor da meta em centavos inteiros não-negativos.</summary>
    public required long ValorMeta { get; init; }
}

/// <summary>
/// Resultado do comando de upsert.
/// </summary>
public sealed record CreateOrUpdateGoalResult(
    GoalDto Goal,
    bool Created);

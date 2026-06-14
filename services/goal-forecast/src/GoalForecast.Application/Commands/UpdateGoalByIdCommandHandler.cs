using GoalForecast.Application.Common;
using GoalForecast.Application.Ports;
using GoalForecast.Domain.Policies;
using GoalForecast.Domain.ValueObjects;
using MediatR;
using AppException = GoalForecast.Application.Common.ApplicationException;

namespace GoalForecast.Application.Commands;

/// <summary>
/// Handler para atualização de valor_meta por ID (PUT /api/v1/goals/{id}).
///
/// Fluxo:
/// 1. Resolve tenant_id do principal autenticado.
/// 2. FindById no repositório → GF-ERR-007 se não encontrado.
/// 3. Autorização: CanWrite para o escopo do aggregate encontrado.
/// 4. ChangeValorMeta no aggregate.
/// 5. Update via repositório (despacha domain events via outbox).
///
/// Mapeia: Req 4, design §8.2, TASK-22.
/// </summary>
public sealed class UpdateGoalByIdCommandHandler : IRequestHandler<UpdateGoalByIdCommand, GoalDto>
{
    private readonly IGoalRepository _repository;

    public UpdateGoalByIdCommandHandler(IGoalRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc/>
    public async Task<GoalDto> Handle(
        UpdateGoalByIdCommand command,
        CancellationToken cancellationToken)
    {
        var principal = command.Principal;
        var tenantId = principal.TenantId;

        // Passo 2: busca por id (Global Query Filter garante isolamento por tenant)
        var goal = await _repository.FindById(tenantId, command.GoalId, cancellationToken)
            ?? throw new AppException("GF-ERR-007", "Meta não encontrada.", 404);

        // Passo 3: autorização de escrita sobre o escopo do aggregate
        var authResult = GoalAuthorizationPolicy.CanWrite(principal, goal.Scope, tenantId);
        if (!authResult.IsAllowed)
            throw new AppException("GF-ERR-006", "Operação não permitida.", 403);

        // Passo 4: atualiza valor_meta
        var novoValor = Money.Of(command.ValorMeta);
        goal.ChangeValorMeta(novoValor);

        // Passo 5: persiste (outbox via repositório)
        await _repository.Update(goal, cancellationToken);

        return new GoalDto(
            Id: goal.Id,
            TenantId: goal.TenantId,
            Scope: goal.Scope.Kind.ToString(),
            BuId: goal.Scope.BuId,
            OwnerId: goal.Scope.OwnerId,
            Year: goal.Period.Year,
            Month: goal.Period.Month,
            ValorMeta: goal.ValorMeta.Cents,
            CreatedAt: goal.CreatedAt,
            UpdatedAt: goal.UpdatedAt);
    }
}

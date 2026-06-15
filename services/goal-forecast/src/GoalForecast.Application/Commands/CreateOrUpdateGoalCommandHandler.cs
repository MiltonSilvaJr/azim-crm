using GoalForecast.Application.Common;
using GoalForecast.Application.Ports;
using GoalForecast.Domain.Aggregates;
using GoalForecast.Domain.Policies;
using GoalForecast.Domain.ValueObjects;
using MediatR;
using AppException = GoalForecast.Application.Common.ApplicationException;

namespace GoalForecast.Application.Commands;

/// <summary>
/// Handler único de escrita do módulo com semântica upsert idempotente.
///
/// Fluxo (design §5.1):
/// <list type="number">
///   <item>Resolve tenant_id do contexto autenticado (nunca do payload).</item>
///   <item>Autorização via <see cref="GoalAuthorizationPolicy"/> (escrita).</item>
///   <item>Se escopo RESPONSAVEL, valida membership via <see cref="IBuMembershipReader"/>.</item>
///   <item><see cref="IGoalRepository.FindByKey"/>: existe → update; não existe → create.</item>
///   <item>Persiste via repositório (Add ou Update) em transação.</item>
///   <item>Domain events despachados pelo repositório via outbox (auditoria).</item>
/// </list>
///
/// Idempotência: N upserts com mesma chave resultam em exatamente 1 registro
/// com o último valorMeta (PBT-01, DD-002).
///
/// Mapeia: Req 1, Req 2, Req 4, PBT-01, DD-002, design §5.1, TASK-09.
/// </summary>
public sealed class CreateOrUpdateGoalCommandHandler
    : IRequestHandler<CreateOrUpdateGoalCommand, CreateOrUpdateGoalResult>
{
    private readonly IGoalRepository _repository;
    private readonly IBuMembershipReader _membershipReader;
    private readonly IGoalForecastMetrics _metrics;

    /// <summary>
    /// Inicializa o handler com as portas de saída necessárias.
    /// </summary>
    public CreateOrUpdateGoalCommandHandler(
        IGoalRepository repository,
        IBuMembershipReader membershipReader,
        IGoalForecastMetrics? metrics = null)
    {
        _repository = repository;
        _membershipReader = membershipReader;
        _metrics = metrics ?? NullGoalForecastMetrics.Instance;
    }

    /// <inheritdoc/>
    public async Task<CreateOrUpdateGoalResult> Handle(
        CreateOrUpdateGoalCommand command,
        CancellationToken cancellationToken)
    {
        var principal = command.Principal;
        var tenantId = principal.TenantId; // INV-4: nunca do payload.

        // Passo 2: construir escopo e autorizar.
        var scope = BuildScope(command);
        var authResult = GoalAuthorizationPolicy.CanWrite(principal, scope, tenantId);
        if (!authResult.IsAllowed)
            throw new AppException("GF-ERR-006", "Operação não permitida.", 403);

        // Passo 3: membership para escopo RESPONSAVEL (Req 1.4, DD-004).
        if (scope.OwnerId.HasValue)
            await ValidateMembership(tenantId, scope.OwnerId.Value, scope.BuId, cancellationToken);

        // Passo 4: find-by-key (upsert).
        var period = new GoalPeriod(command.Year, command.Month);
        var valorMeta = Money.Of(command.ValorMeta, command.Currency);

        var existing = await _repository.FindByKey(
            tenantId, scope.BuId, scope.OwnerId, period.Year, period.Month, cancellationToken);

        bool created;
        Goal goal;

        if (existing is null)
        {
            // Passo 5a: criar novo aggregate.
            goal = Goal.Create(tenantId, scope, period, valorMeta);
            await _repository.Add(goal, cancellationToken);
            created = true;
            // Métrica de criação — sem valorMeta (RNF-7.3)
            _metrics.RecordGoalCreated(tenantId.ToString(), scope.BuId.ToString());
        }
        else
        {
            // Passo 5b: atualizar aggregate existente.
            existing.ChangeValorMeta(valorMeta);
            await _repository.Update(existing, cancellationToken);
            goal = existing;
            created = false;
            // Métrica de atualização — sem delta de valorMeta (RNF-7.3)
            _metrics.RecordGoalUpdated(tenantId.ToString(), scope.BuId.ToString());
        }

        // Passo 6: events despachados pelo repositório (via outbox).
        var dto = MapToDto(goal);
        return new CreateOrUpdateGoalResult(dto, created);
    }

    private static GoalScope BuildScope(CreateOrUpdateGoalCommand command)
    {
        if (string.Equals(command.Scope, "RESPONSAVEL", StringComparison.OrdinalIgnoreCase))
        {
            if (!command.OwnerId.HasValue || command.OwnerId.Value == Guid.Empty)
                throw new Domain.Exceptions.DomainException("GF-ERR-003",
                    "Escopo inconsistente: OwnerId é obrigatório no escopo RESPONSAVEL.");
            return GoalScope.ForResponsavel(command.BuId, command.OwnerId.Value);
        }

        if (string.Equals(command.Scope, "BU", StringComparison.OrdinalIgnoreCase))
        {
            if (command.OwnerId.HasValue)
                throw new Domain.Exceptions.DomainException("GF-ERR-003",
                    "Escopo inconsistente: OwnerId não deve ser informado no escopo BU.");
            return GoalScope.ForBu(command.BuId);
        }

        throw new Domain.Exceptions.DomainException("GF-ERR-003",
            $"Escopo inconsistente: valor de Scope inválido '{command.Scope}'.");
    }

    private async Task ValidateMembership(
        Guid tenantId,
        Guid ownerId,
        Guid buId,
        CancellationToken cancellationToken)
    {
        var specification = new BuMembershipSpecification(ownerId, buId);
        var isMember = await _membershipReader.IsOwnerMemberOfBu(
            tenantId, ownerId, buId, cancellationToken);

        if (!specification.IsSatisfied(isMember))
            throw new AppException("GF-ERR-004",
                "Responsável não pertence à BU.", 422);
    }

    private static GoalDto MapToDto(Goal goal) =>
        new(
            Id: goal.Id,
            TenantId: goal.TenantId,
            Scope: goal.Scope.Kind.ToString(),
            BuId: goal.Scope.BuId,
            OwnerId: goal.Scope.OwnerId,
            Year: goal.Period.Year,
            Month: goal.Period.Month,
            ValorMeta: goal.ValorMeta.Cents,
            Currency: goal.ValorMeta.Currency,
            CreatedAt: goal.CreatedAt,
            UpdatedAt: goal.UpdatedAt);
}

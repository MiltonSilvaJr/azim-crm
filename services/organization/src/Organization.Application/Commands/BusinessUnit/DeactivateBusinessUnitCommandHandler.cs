using MediatR;
using Organization.Application.Ports;

namespace Organization.Application.Commands.BusinessUnit;

/// <summary>
/// Handler para <see cref="DeactivateBusinessUnitCommand"/>.
/// Bloqueia a inativação quando há oportunidades ativas na BU (ORG-ERR-002).
/// Fail-closed: falha do <see cref="IOpportunityCounter"/> também bloqueia a operação.
/// </summary>
public sealed class DeactivateBusinessUnitCommandHandler : IRequestHandler<DeactivateBusinessUnitCommand>
{
    private readonly IBusinessUnitRepository _repository;
    private readonly IOpportunityCounter _opportunityCounter;
    private readonly IEventOutbox _outbox;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    /// <summary>Inicializa o handler com os ports necessários.</summary>
    public DeactivateBusinessUnitCommandHandler(
        IBusinessUnitRepository repository,
        IOpportunityCounter opportunityCounter,
        IEventOutbox outbox,
        ITenantContext tenantContext,
        IClock clock)
    {
        _repository = repository;
        _opportunityCounter = opportunityCounter;
        _outbox = outbox;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task Handle(DeactivateBusinessUnitCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;

        var bu = await _repository.GetByIdAsync(request.BusinessUnitId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Business Unit '{request.BusinessUnitId}' não encontrada.");

        // Verifica oportunidades ativas — fail-closed (ORG-ERR-002)
        var activeCount = await _opportunityCounter.CountActiveAsync(tenantId, request.BusinessUnitId, cancellationToken);
        if (activeCount > 0)
            throw new InvalidOperationException(
                $"Esta BU possui {activeCount} oportunidades ativas. Encerre ou realoque-as antes de inativar. ORG-ERR-002");

        bu.Deactivate(_clock.UtcNow);

        foreach (var domainEvent in bu.DomainEvents)
        {
            await _outbox.EnqueueAsync(domainEvent, tenantId, _tenantContext.CorrelationId, cancellationToken);
        }

        bu.ClearDomainEvents();
        await _repository.SaveAsync(bu, cancellationToken);
    }
}

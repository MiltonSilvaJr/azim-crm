using MediatR;
using Organization.Application.Ports;
using Organization.Domain.Aggregates;
using Organization.Domain.Policies;
using Organization.Domain.ValueObjects;

namespace Organization.Application.Commands.BusinessUnit;

/// <summary>
/// Handler para <see cref="CreateBusinessUnitCommand"/>.
/// Cria a BU com seeds de estágios (DD-002), canais de origem e motivos de perda.
/// Valida unicidade de nome via repositório (ORG-ERR-001).
/// Emite <c>BusinessUnitCreated</c> via <see cref="IEventOutbox"/>.
/// Incrementa métrica <c>bu_created_total</c> (design §11).
/// </summary>
public sealed class CreateBusinessUnitCommandHandler : IRequestHandler<CreateBusinessUnitCommand, Guid>
{
    private readonly IBusinessUnitRepository _repository;
    private readonly IEventOutbox _outbox;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IOrganizationMetrics _metrics;

    /// <summary>Inicializa o handler com os ports necessários.</summary>
    public CreateBusinessUnitCommandHandler(
        IBusinessUnitRepository repository,
        IEventOutbox outbox,
        ITenantContext tenantContext,
        IClock clock,
        IOrganizationMetrics metrics)
    {
        _repository = repository;
        _outbox = outbox;
        _tenantContext = tenantContext;
        _clock = clock;
        _metrics = metrics;
    }

    /// <inheritdoc/>
    public async Task<Guid> Handle(CreateBusinessUnitCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        var name = BusinessUnitName.Create(request.Name);

        // Valida unicidade de nome no tenant (ORG-ERR-001)
        var nameExists = await _repository.ExistsByNameAsync(name.Value, cancellationToken);
        if (nameExists)
            throw new InvalidOperationException(
                $"Já existe uma Business Unit com o nome '{request.Name}'. ORG-ERR-001");

        // Cria o agregado
        var bu = Domain.Aggregates.BusinessUnit.Create(name, tenantId, _clock.UtcNow);

        // Aplica seed de estágios (DD-002)
        foreach (var seed in StageSeedFactory.CreateDefaultStages())
        {
            bu.AddStage(seed.Name, seed.Probability, seed.Category, seed.Position, Guid.NewGuid());
        }

        // Aplica seed de canal de origem padrão
        foreach (var channelName in OriginChannelSeedFactory.CreateDefaultChannels())
        {
            bu.AddOriginChannel(channelName, Guid.NewGuid());
        }

        // Adiciona motivo de perda padrão (requisito de habilitação: ≥1 ativo)
        bu.AddLossReason("Preço", Guid.NewGuid());

        // Publica domain events via Outbox (na mesma transação gerenciada pelo TransactionBehavior)
        foreach (var domainEvent in bu.DomainEvents)
        {
            await _outbox.EnqueueAsync(domainEvent, tenantId, _tenantContext.CorrelationId, cancellationToken);
        }

        bu.ClearDomainEvents();

        await _repository.SaveAsync(bu, cancellationToken);

        _metrics.IncrementBuCreated();

        return bu.Id;
    }
}

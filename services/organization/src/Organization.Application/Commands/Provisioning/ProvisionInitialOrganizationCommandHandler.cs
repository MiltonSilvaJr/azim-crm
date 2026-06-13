using MediatR;
using Organization.Application.Ports;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;
using DomainBusinessUnit = Organization.Domain.Aggregates.BusinessUnit;
using DomainUser = Organization.Domain.Aggregates.User;

namespace Organization.Application.Commands.Provisioning;

/// <summary>
/// Handler para <see cref="ProvisionInitialOrganizationCommand"/>.
/// Consome o evento <c>TenantProvisioned</c>; cria BU inicial com seeds e TAdmin;
/// usa <c>IInboxStore</c> para deduplicação (Req 12, §6.5, §6.6).
/// Reprocessamento idempotente: segundo processamento do mesmo <c>MessageId</c> é ignorado.
/// </summary>
public sealed class ProvisionInitialOrganizationCommandHandler
    : IRequestHandler<ProvisionInitialOrganizationCommand>
{
    private readonly IBusinessUnitRepository _buRepository;
    private readonly IUserRepository _userRepository;
    private readonly IIdentityProvisioner _identityProvisioner;
    private readonly IInboxStore _inboxStore;
    private readonly IEventOutbox _outbox;
    private readonly IClock _clock;
    private readonly ITenantContext _tenantContext;

    private const string EventType = "organization.provisioned";

    /// <summary>Inicializa o handler.</summary>
    public ProvisionInitialOrganizationCommandHandler(
        IBusinessUnitRepository buRepository,
        IUserRepository userRepository,
        IIdentityProvisioner identityProvisioner,
        IInboxStore inboxStore,
        IEventOutbox outbox,
        IClock clock,
        ITenantContext tenantContext)
    {
        _buRepository = buRepository;
        _userRepository = userRepository;
        _identityProvisioner = identityProvisioner;
        _inboxStore = inboxStore;
        _outbox = outbox;
        _clock = clock;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc/>
    public async Task Handle(ProvisionInitialOrganizationCommand request, CancellationToken cancellationToken)
    {
        // Deduplicação via Inbox (PBT-03)
        var alreadyProcessed = await _inboxStore.IsProcessedAsync(
            request.MessageId,
            request.TenantId,
            cancellationToken);

        if (alreadyProcessed)
            return;

        var now = _clock.UtcNow;
        var correlationId = _tenantContext.CorrelationId;

        // Cria BU inicial
        var bu = DomainBusinessUnit.Create(
            BusinessUnitName.Create(request.OrganizationName),
            request.TenantId,
            now);

        // Aplica seeds de estágios (8 estágios Vellus — DD-002)
        foreach (var entry in StageSeedFactory.CreateDefaultStages())
        {
            bu.AddStage(entry.Name, entry.Probability, entry.Category, entry.Position, Guid.NewGuid());
        }

        // Aplica seeds de canais de origem (4 canais)
        foreach (var channelName in OriginChannelSeedFactory.CreateDefaultChannels())
        {
            bu.AddOriginChannel(channelName, Guid.NewGuid());
        }

        // Aplica seed de motivo de perda inicial
        bu.AddLossReason("Outros", Guid.NewGuid());

        // Publica eventos da BU via Outbox
        foreach (var domainEvent in bu.DomainEvents)
            await _outbox.EnqueueAsync(domainEvent, request.TenantId, correlationId, cancellationToken);

        bu.ClearDomainEvents();
        await _buRepository.SaveAsync(bu, cancellationToken);

        // Provisiona identidade do TAdmin inicial
        var identityUid = await _identityProvisioner.ProvisionAsync(
            request.AdminEmail,
            request.AdminDisplayName,
            cancellationToken);

        // Cria usuário TAdmin
        var adminUser = DomainUser.Activate(
            request.AdminEmail,
            request.AdminDisplayName,
            identityUid,
            request.TenantId,
            now);

        adminUser.AssignMembership(bu.Id, Role.TAdmin, Guid.NewGuid());

        // Publica eventos do User via Outbox
        foreach (var domainEvent in adminUser.DomainEvents)
            await _outbox.EnqueueAsync(domainEvent, request.TenantId, correlationId, cancellationToken);

        adminUser.ClearDomainEvents();
        await _userRepository.SaveAsync(adminUser, cancellationToken);

        // Marca mensagem como processada no Inbox (atomicidade garantida pela TransactionBehavior)
        await _inboxStore.MarkProcessedAsync(
            request.MessageId,
            request.TenantId,
            EventType,
            cancellationToken);
    }
}

using FluentAssertions;
using NSubstitute;
using Organization.Application.Commands.Provisioning;
using Organization.Application.Ports;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;
using Xunit;

namespace Organization.Application.Tests.Handlers;

/// <summary>
/// Testes unitários para o handler de provisionamento inicial da organização.
/// </summary>
public sealed class ProvisionInitialOrganizationHandlerTests
{
    private readonly IBusinessUnitRepository _buRepo = Substitute.For<IBusinessUnitRepository>();
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IIdentityProvisioner _identityProvisioner = Substitute.For<IIdentityProvisioner>();
    private readonly IInboxStore _inboxStore = Substitute.For<IInboxStore>();
    private readonly IEventOutbox _outbox = Substitute.For<IEventOutbox>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public ProvisionInitialOrganizationHandlerTests()
    {
        _tenantContext.TenantId.Returns(_tenantId);
        _tenantContext.UserId.Returns(Guid.NewGuid());
        _tenantContext.CorrelationId.Returns(Guid.NewGuid());
        _tenantContext.RolesByBu.Returns(new Dictionary<Guid, string>());
        _clock.UtcNow.Returns(_now);
        _identityProvisioner.ProvisionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("uid-initial-admin");
    }

    [Fact]
    public async Task Provision_FirstTime_CreatesBuWithSeedsAndTAdmin()
    {
        // Arrange — primeira vez: inbox não marcado
        var messageId = Guid.NewGuid().ToString();
        _inboxStore.IsProcessedAsync(messageId, _tenantId, Arg.Any<CancellationToken>()).Returns(false);

        BusinessUnit? savedBu = null;
        Organization.Domain.Aggregates.User? savedUser = null;
        _buRepo.When(r => r.SaveAsync(Arg.Any<BusinessUnit>(), Arg.Any<CancellationToken>()))
            .Do(info => savedBu = info.Arg<BusinessUnit>());
        _userRepo.When(r => r.SaveAsync(Arg.Any<Organization.Domain.Aggregates.User>(), Arg.Any<CancellationToken>()))
            .Do(info => savedUser = info.Arg<Organization.Domain.Aggregates.User>());

        var handler = CreateHandler();
        var command = new ProvisionInitialOrganizationCommand(
            messageId, _tenantId, "admin@org.com", "Administrador", "Organização Principal");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert — BU criada com 8 estágios seed
        savedBu.Should().NotBeNull();
        savedBu!.Stages.Should().HaveCount(8, "seed padrão Vellus tem 8 estágios");
        savedBu.Stages.Should().Contain(s => s.Name == "Lead");
        savedBu.Stages.Should().Contain(s => s.Name == "Ganho");
        savedBu.Stages.Should().Contain(s => s.Name == "Perdido");

        // Assert — TAdmin criado e membership atribuído
        savedUser.Should().NotBeNull();
        savedUser!.Active.Should().BeTrue();
        savedUser.Memberships.Should().ContainSingle(m => m.Role == Role.TAdmin);

        // Assert — inbox marcado
        await _inboxStore.Received(1).MarkProcessedAsync(
            messageId,
            _tenantId,
            "organization.provisioned",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Provision_AlreadyProcessed_SkipsWithoutDuplicating()
    {
        // Arrange — mesmo messageId já processado
        var messageId = Guid.NewGuid().ToString();
        _inboxStore.IsProcessedAsync(messageId, _tenantId, Arg.Any<CancellationToken>()).Returns(true);

        var handler = CreateHandler();
        var command = new ProvisionInitialOrganizationCommand(
            messageId, _tenantId, "admin@org.com", "Administrador", "Organização Principal");

        // Act — segunda execução (mesmo messageId)
        await handler.Handle(command, CancellationToken.None);

        // Assert — nenhuma entidade criada
        await _buRepo.DidNotReceive().SaveAsync(Arg.Any<BusinessUnit>(), Arg.Any<CancellationToken>());
        await _userRepo.DidNotReceive().SaveAsync(Arg.Any<Organization.Domain.Aggregates.User>(), Arg.Any<CancellationToken>());
        await _inboxStore.DidNotReceive().MarkProcessedAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Provision_FirstTime_EmitsEventsViaOutbox()
    {
        // Arrange
        var messageId = Guid.NewGuid().ToString();
        _inboxStore.IsProcessedAsync(messageId, _tenantId, Arg.Any<CancellationToken>()).Returns(false);

        var handler = CreateHandler();
        var command = new ProvisionInitialOrganizationCommand(
            messageId, _tenantId, "admin@org.com", "Administrador", "Organização Principal");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert — Outbox recebeu pelo menos 1 evento (BusinessUnitCreated + UserActivated)
        await _outbox.Received().EnqueueAsync(
            Arg.Any<Organization.Domain.Events.IDomainEvent>(),
            _tenantId,
            _tenantContext.CorrelationId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Provision_BuHasSeedOriginChannels()
    {
        // Arrange
        var messageId = Guid.NewGuid().ToString();
        _inboxStore.IsProcessedAsync(messageId, _tenantId, Arg.Any<CancellationToken>()).Returns(false);

        BusinessUnit? savedBu = null;
        _buRepo.When(r => r.SaveAsync(Arg.Any<BusinessUnit>(), Arg.Any<CancellationToken>()))
            .Do(info => savedBu = info.Arg<BusinessUnit>());

        var handler = CreateHandler();
        var command = new ProvisionInitialOrganizationCommand(
            messageId, _tenantId, "admin@org.com", "Admin", "Org Teste");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert — canais de origem seed incluídos (4 por OriginChannelSeedFactory)
        savedBu.Should().NotBeNull();
        savedBu!.OriginChannels.Should().HaveCount(4);
    }

    // ── Helpers ──

    private ProvisionInitialOrganizationCommandHandler CreateHandler()
        => new(_buRepo, _userRepo, _identityProvisioner, _inboxStore, _outbox, _clock, _tenantContext);
}

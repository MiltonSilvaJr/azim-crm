using FluentAssertions;
using NSubstitute;
using Organization.Application.Commands.BusinessUnit;
using Organization.Application.Ports;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;
using Xunit;

namespace Organization.Application.Tests.Handlers;

/// <summary>
/// Testes unitários para os handlers de Business Unit.
/// </summary>
public sealed class BusinessUnitHandlerTests
{
    private readonly IBusinessUnitRepository _repository = Substitute.For<IBusinessUnitRepository>();
    private readonly IEventOutbox _outbox = Substitute.For<IEventOutbox>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IOpportunityCounter _opportunityCounter = Substitute.For<IOpportunityCounter>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public BusinessUnitHandlerTests()
    {
        _tenantContext.TenantId.Returns(_tenantId);
        _tenantContext.UserId.Returns(Guid.NewGuid());
        _tenantContext.CorrelationId.Returns(Guid.NewGuid());
        _tenantContext.RolesByBu.Returns(new Dictionary<Guid, string>());
        _clock.UtcNow.Returns(_now);
    }

    // ── CreateBusinessUnitCommandHandler ──────────────────────────────────────

    [Fact]
    public async Task CreateBU_WithUniqueName_ReturnsBuId()
    {
        // Arrange
        _repository.ExistsByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var handler = CreateHandler();

        // Act
        var id = await handler.Handle(new CreateBusinessUnitCommand("Vendas SP"), CancellationToken.None);

        // Assert
        id.Should().NotBe(Guid.Empty);
        await _repository.Received(1).SaveAsync(Arg.Any<BusinessUnit>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateBU_WithDuplicateName_ThrowsInvalidOperationException()
    {
        // Arrange
        _repository.ExistsByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(new CreateBusinessUnitCommand("Nome Duplicado"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ORG-ERR-001*");
    }

    [Fact]
    public async Task CreateBU_AppliesSeedStages()
    {
        // Arrange
        _repository.ExistsByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        BusinessUnit? savedBu = null;
        await _repository.SaveAsync(
            Arg.Do<BusinessUnit>(bu => savedBu = bu),
            Arg.Any<CancellationToken>());
        var handler = CreateHandler();

        // Act
        await handler.Handle(new CreateBusinessUnitCommand("Vendas Norte"), CancellationToken.None);

        // Assert
        savedBu.Should().NotBeNull();
        savedBu!.Stages.Should().HaveCount(8, "seed do processo Vellus tem 8 estágios (DD-002)");
        savedBu.Stages.Count(s => s.Category == StageCategory.Won).Should().Be(1);
        savedBu.Stages.Count(s => s.Category == StageCategory.Lost).Should().Be(1);
        savedBu.Stages.Count(s => s.Category == StageCategory.Open).Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task CreateBU_EnqueuesBusinessUnitCreatedEvent()
    {
        // Arrange
        _repository.ExistsByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var handler = CreateHandler();

        // Act
        await handler.Handle(new CreateBusinessUnitCommand("Vendas Sul"), CancellationToken.None);

        // Assert — deve haver eventos no Outbox (pelo menos BusinessUnitCreated + StageConfigured)
        await _outbox.Received().EnqueueAsync(
            Arg.Any<Organization.Domain.Events.IDomainEvent>(),
            _tenantId,
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    // ── RenameBusinessUnitCommandHandler ─────────────────────────────────────

    [Fact]
    public async Task RenameBU_BuNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((BusinessUnit?)null);
        var handler = CreateRenameHandler();

        // Act
        var act = () => handler.Handle(
            new RenameBusinessUnitCommand(Guid.NewGuid(), "Novo Nome"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task RenameBU_DuplicateName_ThrowsInvalidOperationException()
    {
        // Arrange
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Vendas SP"), _tenantId, _now);
        _repository.GetByIdAsync(bu.Id, Arg.Any<CancellationToken>()).Returns(bu);
        _repository.ExistsByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var handler = CreateRenameHandler();

        // Act
        var act = () => handler.Handle(
            new RenameBusinessUnitCommand(bu.Id, "Outro Nome Existente"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ORG-ERR-001*");
    }

    [Fact]
    public async Task RenameBU_WithSameName_DoesNotCheckUniqueness()
    {
        // Arrange
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Vendas SP"), _tenantId, _now);
        _repository.GetByIdAsync(bu.Id, Arg.Any<CancellationToken>()).Returns(bu);
        var handler = CreateRenameHandler();

        // Act — renomear para o mesmo nome não deve checar unicidade
        await handler.Handle(
            new RenameBusinessUnitCommand(bu.Id, "Vendas SP"),
            CancellationToken.None);

        // Assert
        await _repository.DidNotReceive().ExistsByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenameBU_WithNewUniqueName_SavesBu()
    {
        // Arrange
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Vendas SP"), _tenantId, _now);
        _repository.GetByIdAsync(bu.Id, Arg.Any<CancellationToken>()).Returns(bu);
        _repository.ExistsByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var handler = CreateRenameHandler();

        // Act
        await handler.Handle(
            new RenameBusinessUnitCommand(bu.Id, "Vendas Sul"),
            CancellationToken.None);

        // Assert
        await _repository.Received(1).SaveAsync(Arg.Is<BusinessUnit>(b => b.Name.Value == "Vendas Sul"), Arg.Any<CancellationToken>());
    }

    // ── DeactivateBusinessUnitCommandHandler ──────────────────────────────────

    [Fact]
    public async Task DeactivateBU_WithActiveOpportunities_ThrowsInvalidOperationException()
    {
        // Arrange
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Vendas SP"), _tenantId, _now);
        _repository.GetByIdAsync(bu.Id, Arg.Any<CancellationToken>()).Returns(bu);
        _opportunityCounter.CountActiveAsync(_tenantId, bu.Id, Arg.Any<CancellationToken>()).Returns(5);
        var handler = CreateDeactivateHandler();

        // Act
        var act = () => handler.Handle(new DeactivateBusinessUnitCommand(bu.Id), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ORG-ERR-002*");
    }

    [Fact]
    public async Task DeactivateBU_WithNoActiveOpportunities_DeactivatesBu()
    {
        // Arrange
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Vendas SP"), _tenantId, _now);
        _repository.GetByIdAsync(bu.Id, Arg.Any<CancellationToken>()).Returns(bu);
        _opportunityCounter.CountActiveAsync(_tenantId, bu.Id, Arg.Any<CancellationToken>()).Returns(0);
        var handler = CreateDeactivateHandler();

        // Act
        await handler.Handle(new DeactivateBusinessUnitCommand(bu.Id), CancellationToken.None);

        // Assert
        bu.Active.Should().BeFalse();
        await _repository.Received(1).SaveAsync(Arg.Is<BusinessUnit>(b => !b.Active), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateBU_BuNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((BusinessUnit?)null);
        var handler = CreateDeactivateHandler();

        // Act
        var act = () => handler.Handle(new DeactivateBusinessUnitCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Factories ──

    private CreateBusinessUnitCommandHandler CreateHandler()
        => new(_repository, _outbox, _tenantContext, _clock);

    private RenameBusinessUnitCommandHandler CreateRenameHandler()
        => new(_repository, _tenantContext, _clock);

    private DeactivateBusinessUnitCommandHandler CreateDeactivateHandler()
        => new(_repository, _opportunityCounter, _outbox, _tenantContext, _clock);
}

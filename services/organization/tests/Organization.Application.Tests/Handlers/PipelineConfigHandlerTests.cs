using FluentAssertions;
using NSubstitute;
using Organization.Application.Commands.PipelineConfig;
using Organization.Application.Ports;
using Organization.Domain.Aggregates;
using Organization.Domain.Exceptions;
using Organization.Domain.ValueObjects;
using Xunit;

namespace Organization.Application.Tests.Handlers;

/// <summary>
/// Testes unitários para os handlers de configuração de pipeline (Stage, OriginChannel, LossReason).
/// </summary>
public sealed class PipelineConfigHandlerTests
{
    private readonly IBusinessUnitRepository _buRepo = Substitute.For<IBusinessUnitRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    private readonly Guid _tenantId = Guid.NewGuid();

    public PipelineConfigHandlerTests()
    {
        _tenantContext.TenantId.Returns(_tenantId);
        _tenantContext.UserId.Returns(Guid.NewGuid());
        _tenantContext.CorrelationId.Returns(Guid.NewGuid());
        _tenantContext.RolesByBu.Returns(new Dictionary<Guid, string>());
    }

    // ── AddStageCommandHandler ────────────────────────────────────────────────

    [Fact]
    public async Task AddStage_ValidData_AddsStageAndSaves()
    {
        // Arrange
        var bu = CreateActiveBu();
        _buRepo.GetByIdAsync(bu.Id, Arg.Any<CancellationToken>()).Returns(bu);

        var handler = new AddStageCommandHandler(_buRepo, _tenantContext);
        var stageId = Guid.NewGuid();
        var command = new AddStageCommand(bu.Id, stageId, "Qualificação", 50, "open", 10);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        bu.Stages.Should().Contain(s => s.Name == "Qualificação" && s.Position == 10);
        await _buRepo.Received(1).SaveAsync(bu, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddStage_DuplicateName_ThrowsORG_ERR_013()
    {
        // Arrange
        var bu = CreateActiveBu();
        bu.AddStage("Qualificação", Probability.Create(50), StageCategory.Open, 10, Guid.NewGuid());
        _buRepo.GetByIdAsync(bu.Id, Arg.Any<CancellationToken>()).Returns(bu);

        var handler = new AddStageCommandHandler(_buRepo, _tenantContext);
        var command = new AddStageCommand(bu.Id, Guid.NewGuid(), "Qualificação", 50, "open", 20);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*ORG-ERR-013*");
    }

    [Fact]
    public async Task AddStage_DuplicatePosition_ThrowsORG_ERR_014()
    {
        // Arrange
        var bu = CreateActiveBu();
        bu.AddStage("Qualificação", Probability.Create(50), StageCategory.Open, 10, Guid.NewGuid());
        _buRepo.GetByIdAsync(bu.Id, Arg.Any<CancellationToken>()).Returns(bu);

        var handler = new AddStageCommandHandler(_buRepo, _tenantContext);
        var command = new AddStageCommand(bu.Id, Guid.NewGuid(), "Descoberta", 30, "open", 10); // mesma posição

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*ORG-ERR-014*");
    }

    [Fact]
    public async Task RemoveStage_LastTerminalStage_ThrowsORG_ERR_015()
    {
        // Arrange — BU com apenas 1 estágio terminal Won e 1 Lost; tenta remover o único Won
        var bu = CreateActiveBuWithMinimalStages();
        var wonStageId = bu.Stages.First(s => s.Category == StageCategory.Won).Id;
        _buRepo.GetByIdAsync(bu.Id, Arg.Any<CancellationToken>()).Returns(bu);

        var handler = new RemoveStageCommandHandler(_buRepo, _tenantContext);
        var command = new RemoveStageCommand(bu.Id, wonStageId);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*ORG-ERR-015*");
    }

    [Fact]
    public async Task ReorderStages_ValidPositions_UpdatesAndSaves()
    {
        // Arrange
        var bu = CreateActiveBu();
        var stageId1 = Guid.NewGuid();
        var stageId2 = Guid.NewGuid();
        bu.AddStage("Etapa A", Probability.Create(20), StageCategory.Open, 1, stageId1);
        bu.AddStage("Etapa B", Probability.Create(40), StageCategory.Open, 2, stageId2);
        _buRepo.GetByIdAsync(bu.Id, Arg.Any<CancellationToken>()).Returns(bu);

        var handler = new ReorderStagesCommandHandler(_buRepo, _tenantContext);
        var newPositions = new Dictionary<Guid, int> { [stageId1] = 2, [stageId2] = 1 };
        var command = new ReorderStagesCommand(bu.Id, newPositions);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        bu.Stages.First(s => s.Id == stageId1).Position.Should().Be(2);
        bu.Stages.First(s => s.Id == stageId2).Position.Should().Be(1);
        await _buRepo.Received(1).SaveAsync(bu, Arg.Any<CancellationToken>());
    }

    // ── AddOriginChannelCommandHandler ────────────────────────────────────────

    [Fact]
    public async Task AddOriginChannel_ValidData_AddsChannelAndSaves()
    {
        // Arrange
        var bu = CreateActiveBu();
        _buRepo.GetByIdAsync(bu.Id, Arg.Any<CancellationToken>()).Returns(bu);

        var handler = new AddOriginChannelCommandHandler(_buRepo, _tenantContext);
        var channelId = Guid.NewGuid();
        var command = new AddOriginChannelCommand(bu.Id, channelId, "Instagram");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        bu.OriginChannels.Should().Contain(c => c.Name == "Instagram");
        await _buRepo.Received(1).SaveAsync(bu, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateOriginChannel_ExistingChannel_DeactivatesAndSaves()
    {
        // Arrange
        var bu = CreateActiveBu();
        var channelId = Guid.NewGuid();
        bu.AddOriginChannel("Facebook", channelId);
        _buRepo.GetByIdAsync(bu.Id, Arg.Any<CancellationToken>()).Returns(bu);

        var handler = new DeactivateOriginChannelCommandHandler(_buRepo, _tenantContext);
        var command = new DeactivateOriginChannelCommand(bu.Id, channelId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        bu.OriginChannels.First(c => c.Id == channelId).Active.Should().BeFalse();
        await _buRepo.Received(1).SaveAsync(bu, Arg.Any<CancellationToken>());
    }

    // ── AddLossReasonCommandHandler ───────────────────────────────────────────

    [Fact]
    public async Task AddLossReason_ValidData_AddsReasonAndSaves()
    {
        // Arrange
        var bu = CreateActiveBu();
        _buRepo.GetByIdAsync(bu.Id, Arg.Any<CancellationToken>()).Returns(bu);

        var handler = new AddLossReasonCommandHandler(_buRepo, _tenantContext);
        var reasonId = Guid.NewGuid();
        var command = new AddLossReasonCommand(bu.Id, reasonId, "Preço alto");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        bu.LossReasons.Should().Contain(r => r.Name == "Preço alto");
        await _buRepo.Received(1).SaveAsync(bu, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateLossReason_LastActive_ThrowsORG_ERR_017()
    {
        // Arrange — BU com apenas 1 motivo de perda ativo
        var bu = CreateActiveBu();
        var reasonId = Guid.NewGuid();
        bu.AddLossReason("Preço alto", reasonId);
        // Não desativa outros, então é o único ativo
        _buRepo.GetByIdAsync(bu.Id, Arg.Any<CancellationToken>()).Returns(bu);

        var handler = new DeactivateLossReasonCommandHandler(_buRepo, _tenantContext);
        var command = new DeactivateLossReasonCommand(bu.Id, reasonId);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*ORG-ERR-017*");
    }

    [Fact]
    public async Task DeactivateLossReason_WithOthers_DeactivatesAndSaves()
    {
        // Arrange — BU com 2 motivos de perda ativos
        var bu = CreateActiveBu();
        var reasonId1 = Guid.NewGuid();
        var reasonId2 = Guid.NewGuid();
        bu.AddLossReason("Preço alto", reasonId1);
        bu.AddLossReason("Produto inadequado", reasonId2);
        _buRepo.GetByIdAsync(bu.Id, Arg.Any<CancellationToken>()).Returns(bu);

        var handler = new DeactivateLossReasonCommandHandler(_buRepo, _tenantContext);
        var command = new DeactivateLossReasonCommand(bu.Id, reasonId1);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        bu.LossReasons.First(r => r.Id == reasonId1).Active.Should().BeFalse();
        await _buRepo.Received(1).SaveAsync(bu, Arg.Any<CancellationToken>());
    }

    // ── Helpers ──

    private BusinessUnit CreateActiveBu()
        => BusinessUnit.Create(BusinessUnitName.Create("BU Teste"), _tenantId, DateTimeOffset.UtcNow);

    /// <summary>
    /// BU com estágios terminais mínimos (1 Won, 1 Lost) para testar TerminalStagesPolicy.
    /// </summary>
    private BusinessUnit CreateActiveBuWithMinimalStages()
    {
        var bu = CreateActiveBu();
        bu.AddStage("Open", Probability.Create(20), StageCategory.Open, 1, Guid.NewGuid());
        bu.AddStage("Ganho", Probability.Hundred, StageCategory.Won, 2, Guid.NewGuid());
        bu.AddStage("Perdido", Probability.Zero, StageCategory.Lost, 3, Guid.NewGuid());
        return bu;
    }
}

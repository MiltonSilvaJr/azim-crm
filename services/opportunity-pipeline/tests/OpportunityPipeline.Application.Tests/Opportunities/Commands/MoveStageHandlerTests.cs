using FluentAssertions;
using NSubstitute;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Application.Opportunities.Commands;
using OpportunityPipeline.Domain.Opportunities;
using OpportunityPipeline.Domain.Opportunities.Exceptions;
using OpportunityPipeline.Domain.Opportunities.Ports;
using OpportunityPipeline.Domain.Opportunities.Repositories;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;
using Xunit;

namespace OpportunityPipeline.Application.Tests.Opportunities.Commands;

/// <summary>
/// Testes do MoveStageHandler.
/// Cobre: ST-02 — transição inválida → InvalidStageTransitionException; estágio sem data → OP-ERR-005; movimentação válida → evento emitido.
/// Mapeia: Req 5, INV-6, OP-ERR-005,013, TASK-09.
/// </summary>
public sealed class MoveStageHandlerTests
{
    private readonly IOpportunityRepository _repo = Substitute.For<IOpportunityRepository>();
    private readonly IOrganizationReadPort _orgPort = Substitute.For<IOrganizationReadPort>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _buId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    private TenantContext BuildContext()
    {
        var ctx = new TenantContext();
        ctx.Initialize(_tenantId, _buId, _actorId);
        return ctx;
    }

    private MoveStageHandler BuildHandler() =>
        new(_repo, _orgPort, BuildContext(), _uow);

    /// <summary>Cria oportunidade em estágio Open para testes.</summary>
    private static Opportunity BuildOpenOpportunity(Guid tenantId, Guid buId, Guid actorId)
    {
        var stage = new StageRef(Guid.NewGuid(), "Qualificação", StageCategory.Open, 30, 1);
        var channel = new OriginChannelRef(Guid.NewGuid(), "Inbound", false);
        return Opportunity.Create(
            tenantId: tenantId,
            buId: buId,
            accountId: Guid.NewGuid(),
            ownerId: actorId,
            partnerId: null,
            stage: stage,
            originChannel: channel,
            title: "Teste MoveStage",
            contractValue: new ContractValue(new Money(0), new Money(0), 0),
            probability: new Probability(30),
            expectedCloseDate: null,
            notes: null,
            number: new OpportunityNumber("AZ-0001"),
            createdBy: actorId,
            now: DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Transicao_invalida_de_won_para_lost_lanca_InvalidStageTransitionException()
    {
        // Arrange
        var opp = BuildOpenOpportunity(_tenantId, _buId, _actorId);
        opp.Win(_actorId, DateTimeOffset.UtcNow); // open → won
        opp.ClearDomainEvents();

        _repo.GetByIdAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);

        var targetStageId = Guid.NewGuid();
        var lostStage = new StageRef(targetStageId, "Perdido", StageCategory.Lost, 0, 99);
        _orgPort.GetStageRefAsync(_tenantId, targetStageId, Arg.Any<CancellationToken>()).Returns(lostStage);
        _orgPort.GetPropostaEnviadaOrderAsync(_tenantId, _buId, Arg.Any<CancellationToken>()).Returns(5);

        var handler = BuildHandler();
        var command = new MoveStageCommand
        {
            CorrelationId = "test",
            OpportunityId = opp.Id,
            TargetStageId = targetStageId
        };

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidStageTransitionException>();
        await _repo.DidNotReceive().SaveAsync(Arg.Any<Opportunity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Movimentacao_valida_open_to_open_persiste_e_emite_evento()
    {
        // Arrange
        var opp = BuildOpenOpportunity(_tenantId, _buId, _actorId);
        opp.ClearDomainEvents();

        _repo.GetByIdAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);
        _uow.PendingDomainEvents.Returns(new List<Domain.Opportunities.Events.DomainEvent>().AsReadOnly());

        var targetStageId = Guid.NewGuid();
        var nextStage = new StageRef(targetStageId, "Proposta", StageCategory.Open, 50, 2);
        _orgPort.GetStageRefAsync(_tenantId, targetStageId, Arg.Any<CancellationToken>()).Returns(nextStage);
        _orgPort.GetPropostaEnviadaOrderAsync(_tenantId, _buId, Arg.Any<CancellationToken>()).Returns(5);

        var handler = BuildHandler();
        var command = new MoveStageCommand
        {
            CorrelationId = "test",
            OpportunityId = opp.Id,
            TargetStageId = targetStageId
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.ToStageCategory.Should().Be("Open");
        await _repo.Received(1).SaveAsync(opp, Arg.Any<CancellationToken>());
        _uow.Received(1).AddDomainEvents(Arg.Any<IEnumerable<Domain.Opportunities.Events.DomainEvent>>());
    }

    [Fact]
    public async Task Movimentacao_para_estagio_acima_de_proposta_sem_data_lanca_ExpectedCloseDateRequiredException()
    {
        // Arrange
        var opp = BuildOpenOpportunity(_tenantId, _buId, _actorId);
        // opp sem ExpectedCloseDate (null)
        opp.ClearDomainEvents();

        _repo.GetByIdAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);

        var targetStageId = Guid.NewGuid();
        // Estágio com order = 5, propostaEnviadaOrder = 5 → exige data
        var propostaStage = new StageRef(targetStageId, "Proposta Enviada", StageCategory.Open, 60, 5);
        _orgPort.GetStageRefAsync(_tenantId, targetStageId, Arg.Any<CancellationToken>()).Returns(propostaStage);
        _orgPort.GetPropostaEnviadaOrderAsync(_tenantId, _buId, Arg.Any<CancellationToken>()).Returns(5);

        var handler = BuildHandler();
        var command = new MoveStageCommand
        {
            CorrelationId = "test",
            OpportunityId = opp.Id,
            TargetStageId = targetStageId
        };

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ExpectedCloseDateRequiredException>();
    }
}

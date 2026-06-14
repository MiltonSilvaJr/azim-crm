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
/// Testes dos handlers LoseOpportunity e ReopenOpportunity.
/// Cobre: ST-02 RBAC reopen; ST-04 lose sem motivo; reabertura preserva snapshot.
/// Mapeia: Req 10, Req 15, INV-7, RNF 4, OP-ERR-006,008,013, TASK-10.
/// </summary>
public sealed class LoseReopenHandlerTests
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

    private static Opportunity BuildOpenOpportunity(Guid tenantId, Guid buId, Guid actorId)
    {
        var stage = new StageRef(Guid.NewGuid(), "Q", StageCategory.Open, 30, 1);
        var channel = new OriginChannelRef(Guid.NewGuid(), "Inbound", false);
        return Opportunity.Create(
            tenantId, buId, Guid.NewGuid(), actorId, null,
            stage, channel, "Teste Lose/Reopen",
            new ContractValue(new Money(5000), new Money(0), 0),
            new Probability(50), null, null,
            new OpportunityNumber("AZ-0001"), actorId, DateTimeOffset.UtcNow);
    }

    // =========================================================================
    // LoseOpportunityHandler Tests
    // =========================================================================

    [Fact]
    public async Task Lose_sem_loss_reason_id_lanca_ValidationException_com_OP_ERR_006()
    {
        // Validator FluentValidation deve rejeitar antes do handler
        var validator = new LoseOpportunityValidator();
        var command = new LoseOpportunityCommand
        {
            CorrelationId = "test",
            OpportunityId = Guid.NewGuid(),
            LossReasonId = Guid.Empty // inválido
        };

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("OP-ERR-006"));
    }

    [Fact]
    public async Task Lose_com_motivo_valido_encerra_oportunidade_e_emite_OpportunityLost()
    {
        // Arrange
        var opp = BuildOpenOpportunity(_tenantId, _buId, _actorId);
        opp.ClearDomainEvents();

        var lossReasonId = Guid.NewGuid();
        var lossReason = new LossReasonRef(lossReasonId, "Preço");

        _repo.GetByIdAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);
        _orgPort.GetLossReasonRefAsync(_tenantId, lossReasonId, Arg.Any<CancellationToken>()).Returns(lossReason);
        _uow.PendingDomainEvents.Returns(new List<Domain.Opportunities.Events.DomainEvent>().AsReadOnly());

        var handler = new LoseOpportunityHandler(_repo, _orgPort, BuildContext(), _uow);
        var command = new LoseOpportunityCommand
        {
            CorrelationId = "test",
            OpportunityId = opp.Id,
            LossReasonId = lossReasonId
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.OpportunityId.Should().Be(opp.Id);
        result.ClosedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        opp.StageCategory.Should().Be(StageCategory.Lost);
        opp.LossReason.Should().NotBeNull();
        await _repo.Received(1).SaveAsync(opp, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Lose_em_oportunidade_won_lanca_InvalidStageTransitionException()
    {
        // Arrange
        var opp = BuildOpenOpportunity(_tenantId, _buId, _actorId);
        opp.Win(_actorId, DateTimeOffset.UtcNow);
        opp.ClearDomainEvents();

        var lossReasonId = Guid.NewGuid();
        _repo.GetByIdAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);
        _orgPort.GetLossReasonRefAsync(_tenantId, lossReasonId, Arg.Any<CancellationToken>())
            .Returns(new LossReasonRef(lossReasonId, "Motivo"));

        var handler = new LoseOpportunityHandler(_repo, _orgPort, BuildContext(), _uow);
        var command = new LoseOpportunityCommand
        {
            CorrelationId = "test",
            OpportunityId = opp.Id,
            LossReasonId = lossReasonId
        };

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidStageTransitionException>();
    }

    // =========================================================================
    // ReopenOpportunityHandler Tests
    // =========================================================================

    [Fact]
    public async Task Vendedor_em_reopen_deve_ser_bloqueado_pelo_RbacBehavior()
    {
        // ST-02: RBAC verificado pelo RbacBehavior — Vendedor não está em [GestorBU, TenantAdmin]
        // Verificamos via atributo do command
        var attribute = typeof(ReopenOpportunityCommand)
            .GetCustomAttributes(typeof(RequiresRoleAttribute), false)
            .Cast<RequiresRoleAttribute>()
            .FirstOrDefault();

        attribute.Should().NotBeNull("ReopenOpportunityCommand deve ter [RequiresRole]");
        attribute!.AllowedRoles.Should().Contain(UserRole.GestorBU);
        attribute.AllowedRoles.Should().Contain(UserRole.TenantAdmin);
        attribute.AllowedRoles.Should().NotContain(UserRole.Vendedor);
        attribute.AllowedRoles.Should().NotContain(UserRole.Viewer);
    }

    [Fact]
    public async Task Reopen_em_oportunidade_won_restaura_para_open_e_preserva_snapshot()
    {
        // Arrange
        var stage = new StageRef(Guid.NewGuid(), "Q", StageCategory.Open, 30, 1);
        var partnerChannel = new OriginChannelRef(Guid.NewGuid(), "Parceiro", IsPartnerChannel: true);
        var partnerId = Guid.NewGuid();

        var opp = Opportunity.Create(
            _tenantId, _buId, Guid.NewGuid(), _actorId, partnerId,
            stage, partnerChannel, "Opp com Comissão",
            new ContractValue(new Money(50000), new Money(0), 0),
            new Probability(70), null, null,
            new OpportunityNumber("AZ-0001"), _actorId, DateTimeOffset.UtcNow);

        var terms = new CommissionTerms(CommissionRole.Revendedor, 10m, 0m, null, 0);
        opp.SetPartnerCommission(partnerId, terms, DateTimeOffset.UtcNow);
        opp.Win(_actorId, DateTimeOffset.UtcNow);
        opp.ClearDomainEvents();

        var snapshotBefore = opp.Commissions.First(c => c.IsSnapshot);

        _repo.GetByIdAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);
        _uow.PendingDomainEvents.Returns(new List<Domain.Opportunities.Events.DomainEvent>().AsReadOnly());

        var handler = new ReopenOpportunityHandler(_repo, BuildContext(), _uow);
        var command = new ReopenOpportunityCommand
        {
            CorrelationId = "test",
            OpportunityId = opp.Id,
            Reason = "Revisão de contrato"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.PreviousCategory.Should().Be("Won");
        result.SnapshotPreserved.Should().BeTrue();
        opp.StageCategory.Should().Be(StageCategory.Open);
        opp.ClosedAt.Should().BeNull();

        // PBT-11: snapshot com mesmos valores após reabertura
        var snapshotAfter = opp.Commissions.FirstOrDefault(c => c.IsSnapshot);
        snapshotAfter.Should().NotBeNull();
        snapshotAfter!.Id.Should().Be(snapshotBefore.Id);
    }

    [Fact]
    public async Task Reopen_em_oportunidade_open_lanca_InvalidStageTransitionException()
    {
        // Arrange — oportunidade já está open
        var opp = BuildOpenOpportunity(_tenantId, _buId, _actorId);
        opp.ClearDomainEvents();

        _repo.GetByIdAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);

        var handler = new ReopenOpportunityHandler(_repo, BuildContext(), _uow);
        var command = new ReopenOpportunityCommand
        {
            CorrelationId = "test",
            OpportunityId = opp.Id
        };

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidStageTransitionException>();
    }
}

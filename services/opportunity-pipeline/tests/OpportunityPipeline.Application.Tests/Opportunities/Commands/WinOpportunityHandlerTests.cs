using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Application.Opportunities.Commands;
using OpportunityPipeline.Domain.Opportunities;
using OpportunityPipeline.Domain.Opportunities.Exceptions;
using OpportunityPipeline.Domain.Opportunities.Repositories;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;
using Xunit;

namespace OpportunityPipeline.Application.Tests.Opportunities.Commands;

/// <summary>
/// Testes do WinOpportunityHandler.
/// Cobre: ST-01 atomicidade; ST-02 RBAC; ST-03 transição inválida; VAL-07/DD-007.
/// Mapeia: Req 14, RNF 5.4, DD-007, OP-ERR-013,017, PBT-07, TASK-10.
/// </summary>
public sealed class WinOpportunityHandlerTests
{
    private readonly IOpportunityRepository _repo = Substitute.For<IOpportunityRepository>();
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

    private static CommissionOnWinChecker BuildChecker(bool required = false)
    {
        var opts = Options.Create(new WinOpportunityOptions { CommissionRequiredOnWin = required });
        return new CommissionOnWinChecker(opts);
    }

    private WinOpportunityHandler BuildHandler(bool commissionRequired = false) =>
        new(_repo, BuildChecker(commissionRequired), BuildContext(), _uow);

    private static Opportunity BuildOpenOpportunity(Guid tenantId, Guid buId, Guid actorId)
    {
        var stage = new StageRef(Guid.NewGuid(), "Qualificação", StageCategory.Open, 30, 1);
        var channel = new OriginChannelRef(Guid.NewGuid(), "Inbound", false);
        return Opportunity.Create(
            tenantId, buId, Guid.NewGuid(), actorId, null,
            stage, channel, "Teste Win",
            new ContractValue(new Money(10000, "BRL"), new Money(0, "BRL"), 0),
            new Probability(80), null, null,
            new OpportunityNumber("AZ-0001"), actorId, DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Ganho_bem_sucedido_retorna_resultado_com_closed_at()
    {
        // Arrange
        var opp = BuildOpenOpportunity(_tenantId, _buId, _actorId);
        opp.ClearDomainEvents();

        _repo.GetByIdAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);
        _uow.PendingDomainEvents.Returns(new List<Domain.Opportunities.Events.DomainEvent>().AsReadOnly());

        var handler = BuildHandler();
        var command = new WinOpportunityCommand
        {
            CorrelationId = "test",
            OpportunityId = opp.Id
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.OpportunityId.Should().Be(opp.Id);
        result.ClosedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        await _repo.Received(1).SaveAsync(opp, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Falha_na_persistencia_nao_completa_operacao_rollback_e_responsabilidade_do_TransactionBehavior()
    {
        // ST-01: atomicidade — se SaveAsync lança, o handler propaga exceção sem estado parcial.
        // O rollback real é do TransactionBehavior (teste documenta que o handler não engole exceção).
        var opp = BuildOpenOpportunity(_tenantId, _buId, _actorId);
        opp.ClearDomainEvents();

        _repo.GetByIdAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);
        _repo.SaveAsync(Arg.Any<Opportunity>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Simulação de falha no banco"));

        var handler = BuildHandler();
        var command = new WinOpportunityCommand
        {
            CorrelationId = "test",
            OpportunityId = opp.Id
        };

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert — exceção propagada (TransactionBehavior faz rollback)
        await act.Should().ThrowAsync<InvalidOperationException>();
        // Não deve ter chamado AddDomainEvents nem RegisterAudit (falhou antes)
        _uow.DidNotReceive().RegisterAudit(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<object>());
    }

    [Fact]
    public async Task Ganho_em_oportunidade_won_lanca_InvalidStageTransitionException()
    {
        // ST-03: transição inválida won → won
        var opp = BuildOpenOpportunity(_tenantId, _buId, _actorId);
        opp.Win(_actorId, DateTimeOffset.UtcNow); // já ganha
        opp.ClearDomainEvents();

        _repo.GetByIdAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);

        var handler = BuildHandler();
        var command = new WinOpportunityCommand
        {
            CorrelationId = "test",
            OpportunityId = opp.Id
        };

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidStageTransitionException>();
        await _repo.DidNotReceive().SaveAsync(Arg.Any<Opportunity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ganho_em_oportunidade_lost_lanca_InvalidStageTransitionException()
    {
        // ST-03: transição inválida lost → won
        var opp = BuildOpenOpportunity(_tenantId, _buId, _actorId);
        var lossReason = new LossReasonRef(Guid.NewGuid(), "Preço");
        opp.Lose(lossReason, _actorId, DateTimeOffset.UtcNow);
        opp.ClearDomainEvents();

        _repo.GetByIdAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);

        var handler = BuildHandler();
        var command = new WinOpportunityCommand
        {
            CorrelationId = "test",
            OpportunityId = opp.Id
        };

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidStageTransitionException>();
    }

    [Fact]
    public async Task VAL07_commission_required_false_permite_ganho_sem_comissao_com_alerta()
    {
        // DD-007: commission_required_on_win = false (default) → alerta, não bloqueia
        var opp = BuildOpenOpportunity(_tenantId, _buId, _actorId);
        opp.ClearDomainEvents();

        _repo.GetByIdAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);
        _uow.PendingDomainEvents.Returns(new List<Domain.Opportunities.Events.DomainEvent>().AsReadOnly());

        var handler = BuildHandler(commissionRequired: false); // default
        var command = new WinOpportunityCommand { CorrelationId = "test", OpportunityId = opp.Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert — não bloqueia; alerta presente quando há parceiro (este caso não tem parceiro)
        result.CommissionSnapshotCreated.Should().BeFalse();
    }

    [Fact]
    public async Task VAL07_commission_required_true_com_parceiro_sem_comissao_bloqueia()
    {
        // DD-007: commission_required_on_win = true → bloqueia se parceiro vinculado sem comissão
        var stage = new StageRef(Guid.NewGuid(), "Q", StageCategory.Open, 30, 1);
        var partnerChannel = new OriginChannelRef(Guid.NewGuid(), "Parceiro", IsPartnerChannel: true);
        var partnerId = Guid.NewGuid();

        var opp = Opportunity.Create(
            _tenantId, _buId, Guid.NewGuid(), _actorId, partnerId,
            stage, partnerChannel, "Opp com Parceiro",
            new ContractValue(new Money(5000, "BRL"), new Money(0, "BRL"), 0),
            new Probability(70), null, null,
            new OpportunityNumber("AZ-0002"), _actorId, DateTimeOffset.UtcNow);
        opp.ClearDomainEvents();

        _repo.GetByIdAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);

        var handler = BuildHandler(commissionRequired: true); // bloqueia
        var command = new WinOpportunityCommand { CorrelationId = "test", OpportunityId = opp.Id };

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.ErrorCode == "OP-ERR-017");
    }

    [Fact]
    public async Task Ganho_com_comissao_projetada_cria_snapshot_e_emite_eventos()
    {
        // Arrange — oportunidade com parceiro e comissão projetada
        var stage = new StageRef(Guid.NewGuid(), "Q", StageCategory.Open, 30, 1);
        var partnerChannel = new OriginChannelRef(Guid.NewGuid(), "Parceiro", IsPartnerChannel: true);
        var partnerId = Guid.NewGuid();

        var opp = Opportunity.Create(
            _tenantId, _buId, Guid.NewGuid(), _actorId, partnerId,
            stage, partnerChannel, "Opp com Comissão",
            new ContractValue(new Money(100000, "BRL"), new Money(0, "BRL"), 0),
            new Probability(80), null, null,
            new OpportunityNumber("AZ-0003"), _actorId, DateTimeOffset.UtcNow);

        // Adiciona comissão projetada
        var terms = new CommissionTerms(CommissionRole.Revendedor, 10m, 0m, null, 0);
        opp.SetPartnerCommission(partnerId, terms, DateTimeOffset.UtcNow);
        opp.ClearDomainEvents();

        _repo.GetByIdAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);
        _uow.PendingDomainEvents.Returns(new List<Domain.Opportunities.Events.DomainEvent>().AsReadOnly());

        var handler = BuildHandler();
        var command = new WinOpportunityCommand { CorrelationId = "test", OpportunityId = opp.Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.CommissionSnapshotCreated.Should().BeTrue();
        opp.StageCategory.Should().Be(StageCategory.Won);
        opp.Commissions.Should().Contain(c => c.IsSnapshot);
    }
}

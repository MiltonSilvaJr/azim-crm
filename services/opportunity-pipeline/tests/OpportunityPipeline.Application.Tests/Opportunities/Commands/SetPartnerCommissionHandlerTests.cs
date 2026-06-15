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
/// Testes do SetPartnerCommissionHandler.
/// Cobre: ST-01 409 se snapshot existe; ST-03 mutual exclusão; pré-preenchimento defaults.
/// Mapeia: Req 11, Req 12, OP-ERR-004,014,015, design §5.3, TASK-11.
/// </summary>
public sealed class SetPartnerCommissionHandlerTests
{
    private readonly IOpportunityRepository _repo = Substitute.For<IOpportunityRepository>();
    private readonly IPartnerReadPort _partnerPort = Substitute.For<IPartnerReadPort>();
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

    private SetPartnerCommissionHandler BuildHandler() =>
        new(_repo, _partnerPort, BuildContext(), _uow);

    private static Opportunity BuildOpenOpportunityWithPartner(Guid tenantId, Guid buId, Guid actorId, Guid partnerId)
    {
        var stage = new StageRef(Guid.NewGuid(), "Q", StageCategory.Open, 30, 1);
        var channel = new OriginChannelRef(Guid.NewGuid(), "Parceiro", IsPartnerChannel: true);
        return Opportunity.Create(
            tenantId, buId, Guid.NewGuid(), actorId, partnerId,
            stage, channel, "Opp com Parceiro",
            new ContractValue(new Money(100000, "BRL"), new Money(0, "BRL"), 0),
            new Probability(60), null, null,
            new OpportunityNumber("AZ-0001"), actorId, DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Tentativa_snapshot_em_oportunidade_com_snapshot_existente_lanca_SnapshotImmutableException()
    {
        // ST-01: 409 Conflict — snapshot já existe após Win()
        var partnerId = Guid.NewGuid();
        var opp = BuildOpenOpportunityWithPartner(_tenantId, _buId, _actorId, partnerId);

        var terms = new CommissionTerms(CommissionRole.Revendedor, 10m, 0m, null, 0);
        opp.SetPartnerCommission(partnerId, terms, DateTimeOffset.UtcNow);
        opp.Win(_actorId, DateTimeOffset.UtcNow); // cria snapshot
        opp.ClearDomainEvents();

        _repo.GetByIdAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);

        var handler = BuildHandler();
        var command = new SetPartnerCommissionCommand
        {
            CorrelationId = "test",
            OpportunityId = opp.Id,
            PartnerId = partnerId,
            Role = CommissionRole.Revendedor,
            PctSetup = 5m
        };

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<SnapshotImmutableException>();
    }

    [Fact]
    public async Task Parceiro_inexistente_lanca_ValidationException_com_OP_ERR_015()
    {
        // ST-03: parceiro não existe
        var partnerId = Guid.NewGuid();
        var opp = BuildOpenOpportunityWithPartner(_tenantId, _buId, _actorId, partnerId);
        opp.ClearDomainEvents();

        _repo.GetByIdAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);
        _partnerPort.PartnerExistsAsync(_tenantId, partnerId, Arg.Any<CancellationToken>()).Returns(false);

        var handler = BuildHandler();
        var command = new SetPartnerCommissionCommand
        {
            CorrelationId = "test",
            OpportunityId = opp.Id,
            PartnerId = partnerId,
            Role = CommissionRole.Revendedor,
            PctSetup = 10m
        };

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.ErrorCode == "OP-ERR-015");
    }

    [Fact]
    public async Task SetCommission_com_percentuais_persiste_e_retorna_resultado()
    {
        // Cenário base: comissão por percentual de setup
        var partnerId = Guid.NewGuid();
        var opp = BuildOpenOpportunityWithPartner(_tenantId, _buId, _actorId, partnerId);
        opp.ClearDomainEvents();

        _repo.GetByIdAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);
        _partnerPort.PartnerExistsAsync(_tenantId, partnerId, Arg.Any<CancellationToken>()).Returns(true);
        _uow.PendingDomainEvents.Returns(new List<Domain.Opportunities.Events.DomainEvent>().AsReadOnly());

        var handler = BuildHandler();
        var command = new SetPartnerCommissionCommand
        {
            CorrelationId = "test",
            OpportunityId = opp.Id,
            PartnerId = partnerId,
            Role = CommissionRole.Revendedor,
            PctSetup = 10m,
            PctRecorrente = 0m
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.OpportunityId.Should().Be(opp.Id);
        result.IsSnapshot.Should().BeFalse();
        await _repo.Received(1).SaveAsync(opp, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetCommission_com_valor_fixo_persiste_e_retorna_resultado()
    {
        // Comissão por valor fixo em centavos
        var partnerId = Guid.NewGuid();
        var opp = BuildOpenOpportunityWithPartner(_tenantId, _buId, _actorId, partnerId);
        opp.ClearDomainEvents();

        _repo.GetByIdAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);
        _partnerPort.PartnerExistsAsync(_tenantId, partnerId, Arg.Any<CancellationToken>()).Returns(true);
        _uow.PendingDomainEvents.Returns(new List<Domain.Opportunities.Events.DomainEvent>().AsReadOnly());

        var handler = BuildHandler();
        var command = new SetPartnerCommissionCommand
        {
            CorrelationId = "test",
            OpportunityId = opp.Id,
            PartnerId = partnerId,
            Role = CommissionRole.Revendedor,
            ValorFixoCents = 5000L
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.OpportunityId.Should().Be(opp.Id);
        await _repo.Received(1).SaveAsync(opp, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetCommission_com_UsePartnerDefaults_usa_defaults_do_parceiro()
    {
        // UsePartnerDefaults=true preenche pct_setup/pct_recorrente com defaults do parceiro
        var partnerId = Guid.NewGuid();
        var opp = BuildOpenOpportunityWithPartner(_tenantId, _buId, _actorId, partnerId);
        opp.ClearDomainEvents();

        var defaults = new CommissionDefaults(PctSetup: 15m, PctRecorrente: 5m);
        _repo.GetByIdAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);
        _partnerPort.PartnerExistsAsync(_tenantId, partnerId, Arg.Any<CancellationToken>()).Returns(true);
        _partnerPort.GetCommissionDefaultsAsync(_tenantId, partnerId, Arg.Any<CancellationToken>()).Returns(defaults);
        _uow.PendingDomainEvents.Returns(new List<Domain.Opportunities.Events.DomainEvent>().AsReadOnly());

        var handler = BuildHandler();
        var command = new SetPartnerCommissionCommand
        {
            CorrelationId = "test",
            OpportunityId = opp.Id,
            PartnerId = partnerId,
            Role = CommissionRole.Revendedor,
            UsePartnerDefaults = true
            // pct_setup=0 → será substituído por 15m do partner
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.OpportunityId.Should().Be(opp.Id);
        await _partnerPort.Received(1).GetCommissionDefaultsAsync(_tenantId, partnerId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Validator_mutual_exclusao_valor_fixo_e_percentuais_retorna_erro_OP_ERR_014()
    {
        // ST-03: mutual exclusão — valor_fixo + pct_setup ao mesmo tempo
        var validator = new SetPartnerCommissionValidator();
        var command = new SetPartnerCommissionCommand
        {
            CorrelationId = "test",
            OpportunityId = Guid.NewGuid(),
            PartnerId = Guid.NewGuid(),
            Role = CommissionRole.Revendedor,
            ValorFixoCents = 5000L,
            PctSetup = 10m // inválido em conjunto com ValorFixoCents
        };

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("OP-ERR-014"));
    }
}

using FluentAssertions;
using NSubstitute;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Application.Opportunities.Commands;
using OpportunityPipeline.Domain.Opportunities;
using OpportunityPipeline.Domain.Opportunities.Ports;
using OpportunityPipeline.Domain.Opportunities.Repositories;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;
using Xunit;

namespace OpportunityPipeline.Application.Tests.Opportunities.Commands;

/// <summary>
/// Testes do CreateOpportunityHandler.
/// Cobre: ST-01 — campos obrigatórios, owner inválido, canal Parceiro sem parceiro, criação bem-sucedida.
/// Mapeia: Req 1, Req 2, Req 3, Req 4, OP-ERR-001,002,003,004, TASK-09.
/// </summary>
public sealed class CreateOpportunityHandlerTests
{
    private readonly IOpportunityRepository _repo = Substitute.For<IOpportunityRepository>();
    private readonly IOpportunityNumberGenerator _numGen = Substitute.For<IOpportunityNumberGenerator>();
    private readonly IOrganizationReadPort _orgPort = Substitute.For<IOrganizationReadPort>();
    private readonly IAccountReadPort _accPort = Substitute.For<IAccountReadPort>();
    private readonly IPartnerReadPort _partnerPort = Substitute.For<IPartnerReadPort>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _buId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _stageId = Guid.NewGuid();
    private readonly Guid _channelId = Guid.NewGuid();

    private static readonly StageRef DefaultStage = new(
        Guid.NewGuid(), "Qualificação", StageCategory.Open, 30, 1);

    private static readonly OriginChannelRef DefaultChannel = new(
        Guid.NewGuid(), "Inbound", IsPartnerChannel: false);

    private TenantContext BuildContext()
    {
        var ctx = new TenantContext();
        ctx.Initialize(_tenantId, _buId, _actorId);
        return ctx;
    }

    private CreateOpportunityHandler BuildHandler(TenantContext? ctx = null)
    {
        return new CreateOpportunityHandler(
            _repo, _numGen, _orgPort, _accPort, _partnerPort,
            ctx ?? BuildContext(), _uow);
    }

    private CreateOpportunityCommand ValidCommand(
        Guid? ownerId = null,
        Guid? channelId = null,
        Guid? stageId = null,
        Guid? accountId = null,
        Guid? partnerId = null) => new()
    {
        CorrelationId = "test-correlation",
        AccountId = accountId ?? _accountId,
        BuId = _buId,
        StageId = stageId ?? _stageId,
        OriginChannelId = channelId ?? _channelId,
        OwnerId = ownerId ?? _ownerId,
        Title = "Oportunidade Teste",
        ValorSetupCents = 10000,
        ValorMensalCents = 5000,
        DuracaoMeses = 12,
        PartnerId = partnerId
    };

    private void SetupDefaults()
    {
        _accPort.AccountExistsAsync(_tenantId, _accountId, Arg.Any<CancellationToken>()).Returns(true);
        _orgPort.ValidateOwnerMembershipAsync(_tenantId, _buId, _ownerId, Arg.Any<CancellationToken>()).Returns(true);
        _orgPort.GetStageRefAsync(_tenantId, _stageId, Arg.Any<CancellationToken>()).Returns(DefaultStage);
        _orgPort.GetOriginChannelRefAsync(_tenantId, _channelId, Arg.Any<CancellationToken>()).Returns(DefaultChannel);
        _numGen.NextAsync(_tenantId, Arg.Any<CancellationToken>()).Returns(new OpportunityNumber("AZ-0001"));
        _uow.PendingDomainEvents.Returns(new List<Domain.Opportunities.Events.DomainEvent>().AsReadOnly());
    }

    [Fact]
    public async Task Criacao_bem_sucedida_retorna_resultado_com_number_formatado_AZ_NNNN()
    {
        // Arrange
        SetupDefaults();
        var handler = BuildHandler();
        var command = ValidCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.OpportunityNumber.Should().Be("AZ-0001");
        result.TenantId.Should().Be(_tenantId);
        result.Id.Should().NotBe(Guid.Empty);

        await _repo.Received(1).AddAsync(Arg.Any<Opportunity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Owner_invalido_lanca_ValidationException_com_OP_ERR_002()
    {
        // Arrange
        SetupDefaults();
        _orgPort.ValidateOwnerMembershipAsync(_tenantId, _buId, _ownerId, Arg.Any<CancellationToken>()).Returns(false);
        var handler = BuildHandler();
        var command = ValidCommand();

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.ErrorCode == "OP-ERR-002");

        await _repo.DidNotReceive().AddAsync(Arg.Any<Opportunity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Canal_Parceiro_sem_partner_id_lanca_ValidationException_com_OP_ERR_004()
    {
        // Arrange
        SetupDefaults();
        var partnerChannelId = Guid.NewGuid();
        var partnerChannel = new OriginChannelRef(partnerChannelId, "Parceiro", IsPartnerChannel: true);
        _orgPort.GetOriginChannelRefAsync(_tenantId, partnerChannelId, Arg.Any<CancellationToken>()).Returns(partnerChannel);
        var handler = BuildHandler();
        var command = ValidCommand(channelId: partnerChannelId); // sem PartnerId

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.ErrorCode == "OP-ERR-004");
    }

    [Fact]
    public async Task Conta_inexistente_lanca_ValidationException_com_OP_ERR_010()
    {
        // Arrange
        SetupDefaults();
        var unknownAccountId = Guid.NewGuid();
        _accPort.AccountExistsAsync(_tenantId, unknownAccountId, Arg.Any<CancellationToken>()).Returns(false);
        var handler = BuildHandler();
        var command = ValidCommand(accountId: unknownAccountId);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.ErrorCode == "OP-ERR-010");
    }

    [Fact]
    public async Task Canal_Parceiro_com_parceiro_valido_cria_oportunidade_com_sucesso()
    {
        // Arrange
        SetupDefaults();
        var partnerChannelId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var partnerChannel = new OriginChannelRef(partnerChannelId, "Parceiro", IsPartnerChannel: true);
        _orgPort.GetOriginChannelRefAsync(_tenantId, partnerChannelId, Arg.Any<CancellationToken>()).Returns(partnerChannel);
        _partnerPort.PartnerExistsAsync(_tenantId, partnerId, Arg.Any<CancellationToken>()).Returns(true);
        var handler = BuildHandler();
        var command = ValidCommand(channelId: partnerChannelId, partnerId: partnerId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        await _repo.Received(1).AddAsync(Arg.Any<Opportunity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Probabilidade_usa_default_do_estagio_quando_nao_informada()
    {
        // Arrange
        SetupDefaults();
        Opportunity? persisted = null;
        await _repo.AddAsync(Arg.Do<Opportunity>(o => persisted = o), Arg.Any<CancellationToken>());

        var handler = BuildHandler();
        var command = ValidCommand(); // Probabilidade = null → deve usar DefaultStage.DefaultProbability = 30

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.Probability.Value.Should().Be(30); // default do estágio
    }

    [Fact]
    public async Task Probabilidade_customizada_e_aplicada_quando_informada()
    {
        // Arrange
        SetupDefaults();
        Opportunity? persisted = null;
        await _repo.AddAsync(Arg.Do<Opportunity>(o => persisted = o), Arg.Any<CancellationToken>());

        var handler = BuildHandler();
        var command = ValidCommand() with { Probabilidade = 75 };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        persisted!.Probability.Value.Should().Be(75);
    }
}

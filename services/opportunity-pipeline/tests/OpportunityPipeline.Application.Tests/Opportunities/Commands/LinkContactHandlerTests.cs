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
/// Testes dos handlers LinkContact e UnlinkContact.
/// Cobre: ST-02 OP-ERR-016 (contato outra conta); desvinculação único principal.
/// Mapeia: Req 16, INV-11, OP-ERR-009,016, TASK-11.
/// </summary>
public sealed class LinkContactHandlerTests
{
    private readonly IOpportunityRepository _repo = Substitute.For<IOpportunityRepository>();
    private readonly IAccountReadPort _accountPort = Substitute.For<IAccountReadPort>();
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
            stage, channel, "Opp Link Contact",
            new ContractValue(new Money(10000), new Money(0), 0),
            new Probability(50), null, null,
            new OpportunityNumber("AZ-0001"), actorId, DateTimeOffset.UtcNow);
    }

    // =========================================================================
    // LinkContactHandler Tests
    // =========================================================================

    [Fact]
    public async Task LinkContact_com_contato_de_outra_conta_lanca_ValidationException_OP_ERR_016()
    {
        // ST-02: contato não pertence à conta da oportunidade
        var opp = BuildOpenOpportunity(_tenantId, _buId, _actorId);
        var contactId = Guid.NewGuid();
        opp.ClearDomainEvents();

        _repo.GetByIdAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);
        _accountPort.ContactBelongsToAccountAsync(
            _tenantId, opp.AccountId, contactId, Arg.Any<CancellationToken>())
            .Returns(false); // contato de outra conta

        var handler = new LinkContactHandler(_repo, _accountPort, BuildContext(), _uow);
        var command = new LinkContactCommand
        {
            CorrelationId = "test",
            OpportunityId = opp.Id,
            ContactId = contactId,
            IsPrimary = true
        };

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.ErrorCode == "OP-ERR-016");
    }

    [Fact]
    public async Task LinkContact_com_contato_valido_vincula_e_retorna_total_de_contatos()
    {
        // Cenário base: contato válido vinculado com sucesso
        var opp = BuildOpenOpportunity(_tenantId, _buId, _actorId);
        var contactId = Guid.NewGuid();
        opp.ClearDomainEvents();

        _repo.GetByIdAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);
        _accountPort.ContactBelongsToAccountAsync(
            _tenantId, opp.AccountId, contactId, Arg.Any<CancellationToken>())
            .Returns(true);
        _uow.PendingDomainEvents.Returns(new List<Domain.Opportunities.Events.DomainEvent>().AsReadOnly());

        var handler = new LinkContactHandler(_repo, _accountPort, BuildContext(), _uow);
        var command = new LinkContactCommand
        {
            CorrelationId = "test",
            OpportunityId = opp.Id,
            ContactId = contactId,
            IsPrimary = true
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.OpportunityId.Should().Be(opp.Id);
        result.TotalContacts.Should().Be(1);
        await _repo.Received(1).SaveAsync(opp, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LinkContact_validator_rejeita_contact_id_vazio_com_OP_ERR_016()
    {
        var validator = new LinkContactValidator();
        var command = new LinkContactCommand
        {
            CorrelationId = "test",
            OpportunityId = Guid.NewGuid(),
            ContactId = Guid.Empty // inválido
        };

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("OP-ERR-016"));
    }

    // =========================================================================
    // UnlinkContactHandler Tests
    // =========================================================================

    [Fact]
    public async Task UnlinkContact_do_contato_principal_quando_ha_outros_contatos_lanca_PrimaryContactRequiredException()
    {
        // ST-02: INV-11 — remover principal quando há outros contatos sem promoção deve falhar.
        // O domínio exige que haja exatamente 1 principal quando há contatos.
        var opp = BuildOpenOpportunity(_tenantId, _buId, _actorId);
        var contact1 = Guid.NewGuid(); // será principal
        var contact2 = Guid.NewGuid(); // secundário

        _repo.GetByIdAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);
        _accountPort.ContactBelongsToAccountAsync(_tenantId, opp.AccountId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _uow.PendingDomainEvents.Returns(new List<Domain.Opportunities.Events.DomainEvent>().AsReadOnly());

        // Vincula dois contatos
        var linkHandler = new LinkContactHandler(_repo, _accountPort, BuildContext(), _uow);
        await linkHandler.Handle(new LinkContactCommand
        {
            CorrelationId = "test",
            OpportunityId = opp.Id,
            ContactId = contact1,
            IsPrimary = true
        }, CancellationToken.None);

        await linkHandler.Handle(new LinkContactCommand
        {
            CorrelationId = "test",
            OpportunityId = opp.Id,
            ContactId = contact2,
            IsPrimary = false
        }, CancellationToken.None);

        opp.ClearDomainEvents();

        // Tenta desvincular o contato principal (com outro secundário existindo) — sem PromotePrimary
        var unlinkHandler = new UnlinkContactHandler(_repo, BuildContext(), _uow);
        var command = new UnlinkContactCommand
        {
            CorrelationId = "test",
            OpportunityId = opp.Id,
            ContactId = contact1 // principal; contact2 existe mas não é principal → domínio bloqueia
        };

        // Act
        var act = () => unlinkHandler.Handle(command, CancellationToken.None);

        // Assert — domínio lança PrimaryContactRequiredException (INV-11)
        await act.Should().ThrowAsync<PrimaryContactRequiredException>();
    }

    [Fact]
    public async Task UnlinkContact_de_contato_nao_principal_remove_com_sucesso()
    {
        // Dado dois contatos, o não-principal pode ser removido
        var opp = BuildOpenOpportunity(_tenantId, _buId, _actorId);
        var contact1 = Guid.NewGuid(); // principal
        var contact2 = Guid.NewGuid(); // secundário

        _repo.GetByIdAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);
        _accountPort.ContactBelongsToAccountAsync(_tenantId, opp.AccountId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _uow.PendingDomainEvents.Returns(new List<Domain.Opportunities.Events.DomainEvent>().AsReadOnly());

        // Vincula dois contatos
        var linkHandler = new LinkContactHandler(_repo, _accountPort, BuildContext(), _uow);
        await linkHandler.Handle(new LinkContactCommand
        {
            CorrelationId = "test",
            OpportunityId = opp.Id,
            ContactId = contact1,
            IsPrimary = true
        }, CancellationToken.None);

        await linkHandler.Handle(new LinkContactCommand
        {
            CorrelationId = "test",
            OpportunityId = opp.Id,
            ContactId = contact2,
            IsPrimary = false
        }, CancellationToken.None);

        opp.ClearDomainEvents();

        // Remove contato secundário
        var unlinkHandler = new UnlinkContactHandler(_repo, BuildContext(), _uow);
        var result = await unlinkHandler.Handle(new UnlinkContactCommand
        {
            CorrelationId = "test",
            OpportunityId = opp.Id,
            ContactId = contact2
        }, CancellationToken.None);

        // Assert
        result.RemainingContacts.Should().Be(1);
        opp.Contacts.Should().HaveCount(1);
        opp.Contacts.Should().Contain(c => c.ContactId == contact1);
    }
}

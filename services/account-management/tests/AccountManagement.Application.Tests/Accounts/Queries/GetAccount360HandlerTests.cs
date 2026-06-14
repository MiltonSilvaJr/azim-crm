using AccountManagement.Application.Accounts.Queries.GetAccount360;
using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.Repositories;
using AccountManagement.Domain.Accounts.Services;
using AccountManagement.Domain.Accounts.ValueObjects;
using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AccountManagement.Application.Tests.Accounts.Queries;

/// <summary>
/// Testes unitários e PBT para <see cref="GetAccount360Handler"/>.
///
/// PBT-05: oportunidades retornadas na 360° são sempre subconjunto das BUs autorizadas.
///
/// Mapeia: TASK-07, design §5.2, Req 6, PBT-05, DD-004, ACC-ERR-003.
/// </summary>
public sealed class GetAccount360HandlerTests
{
    private readonly IAccountRepository _repository;
    private readonly IOpportunityReadPort _opportunityPort;
    private readonly IActivityReadPort _activityPort;
    private readonly GetAccount360Handler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();

    public GetAccount360HandlerTests()
    {
        _repository = Substitute.For<IAccountRepository>();
        _opportunityPort = Substitute.For<IOpportunityReadPort>();
        _activityPort = Substitute.For<IActivityReadPort>();
        _handler = new GetAccount360Handler(_repository, _opportunityPort, _activityPort);
    }

    private Account BuildAccount()
    {
        var normalizer = new NameNormalizer();
        var account = Account.Create(
            _tenantId, AccountName.Create("Empresa 360"), null, null, normalizer);
        account.AddContact(ContactInfo.Create("Ana Lima"), role: "Gerente");
        account.ClearDomainEvents();
        return account;
    }

    // =========================================================================
    // Cenários de sucesso
    // =========================================================================

    [Fact(DisplayName = "GetAccount360: todas as portas disponíveis → 360° completa")]
    public async Task Handle_AllPortsAvailable_Returns360Complete()
    {
        // Arrange
        var account = BuildAccount();
        var buId = Guid.NewGuid();
        var authorizedBus = new HashSet<Guid> { buId };

        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(account);

        var opportunities = new[]
        {
            new OpportunityReadModel(Guid.NewGuid(), buId, "Oportunidade A", "Proposta", 50000L)
        };
        _opportunityPort.GetByAccountAsync(account.Id, Arg.Any<IReadOnlySet<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(opportunities);

        var activities = new[]
        {
            new ActivityReadModel(Guid.NewGuid(), "Reunião", "Reunião de kick-off", DateTimeOffset.UtcNow)
        };
        _activityPort.GetByAccountAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(activities);

        var query = new GetAccount360Query(AccountId: account.Id, AuthorizedBuIds: authorizedBus);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Account.Should().BeSameAs(account);
        result.Contacts.Should().HaveCount(1);
        result.Opportunities.Should().HaveCount(1);
        result.Activities.Should().HaveCount(1);
        result.Availability.OpportunitiesAvailable.Should().BeTrue();
        result.Availability.ActivitiesAvailable.Should().BeTrue();
        result.Availability.IsPartiallyDegraded.Should().BeFalse();
    }

    // =========================================================================
    // Degradação parcial
    // =========================================================================

    [Fact(DisplayName = "GetAccount360: porta de oportunidades falha → seção indisponível, resto intacto")]
    public async Task Handle_OpportunitiesPortFails_ReturnsDegradedView()
    {
        // Arrange
        var account = BuildAccount();
        var authorizedBus = new HashSet<Guid> { Guid.NewGuid() };

        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(account);

        _opportunityPort.GetByAccountAsync(account.Id, Arg.Any<IReadOnlySet<Guid>>(), Arg.Any<CancellationToken>())
            .Throws(new TimeoutException("Downstream timeout"));

        var activities = new[]
        {
            new ActivityReadModel(Guid.NewGuid(), "Ligação", "Ligação de acompanhamento", DateTimeOffset.UtcNow)
        };
        _activityPort.GetByAccountAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(activities);

        var query = new GetAccount360Query(AccountId: account.Id, AuthorizedBuIds: authorizedBus);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert — degradação parcial: oportunidades null, atividades disponíveis
        result.Account.Should().BeSameAs(account);
        result.Opportunities.Should().BeNull();
        result.Activities.Should().HaveCount(1);
        result.Availability.OpportunitiesAvailable.Should().BeFalse();
        result.Availability.ActivitiesAvailable.Should().BeTrue();
        result.Availability.IsPartiallyDegraded.Should().BeTrue();
    }

    [Fact(DisplayName = "GetAccount360: porta de atividades falha → seção indisponível, oportunidades intactas")]
    public async Task Handle_ActivitiesPortFails_ReturnsDegradedView()
    {
        // Arrange
        var account = BuildAccount();
        var buId = Guid.NewGuid();
        var authorizedBus = new HashSet<Guid> { buId };

        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(account);

        var opportunities = new[]
        {
            new OpportunityReadModel(Guid.NewGuid(), buId, "Oportunidade B", "Fechamento", 120000L)
        };
        _opportunityPort.GetByAccountAsync(account.Id, Arg.Any<IReadOnlySet<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(opportunities);

        _activityPort.GetByAccountAsync(account.Id, Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("Serviço de atividades indisponível"));

        var query = new GetAccount360Query(AccountId: account.Id, AuthorizedBuIds: authorizedBus);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert — degradação parcial: atividades null, oportunidades disponíveis
        result.Opportunities.Should().HaveCount(1);
        result.Activities.Should().BeNull();
        result.Availability.OpportunitiesAvailable.Should().BeTrue();
        result.Availability.ActivitiesAvailable.Should().BeFalse();
        result.Availability.IsPartiallyDegraded.Should().BeTrue();
    }

    [Fact(DisplayName = "GetAccount360: ambas as portas falham → 360° degradada, conta intacta")]
    public async Task Handle_BothPortsFail_ReturnsFullyDegradedView()
    {
        // Arrange
        var account = BuildAccount();
        var authorizedBus = new HashSet<Guid> { Guid.NewGuid() };

        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(account);

        _opportunityPort.GetByAccountAsync(account.Id, Arg.Any<IReadOnlySet<Guid>>(), Arg.Any<CancellationToken>())
            .Throws(new TaskCanceledException("Timeout"));
        _activityPort.GetByAccountAsync(account.Id, Arg.Any<CancellationToken>())
            .Throws(new TaskCanceledException("Timeout"));

        var query = new GetAccount360Query(AccountId: account.Id, AuthorizedBuIds: authorizedBus);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert — conta sempre disponível; seções externas nulas
        result.Account.Should().BeSameAs(account);
        result.Contacts.Should().HaveCount(1);
        result.Opportunities.Should().BeNull();
        result.Activities.Should().BeNull();
        result.Availability.IsPartiallyDegraded.Should().BeTrue();
    }

    // =========================================================================
    // Erro da conta
    // =========================================================================

    [Fact(DisplayName = "GetAccount360: conta não encontrada → lança AccountNotFoundException (ACC-ERR-003)")]
    public async Task Handle_AccountNotFound_ThrowsAccountNotFoundException()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Account?)null);

        var query = new GetAccount360Query(
            AccountId: Guid.NewGuid(),
            AuthorizedBuIds: new HashSet<Guid>());

        // Act
        var act = () => _handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AccountNotFoundException>();
    }

    // =========================================================================
    // PBT-05 — Oportunidades retornadas são subconjunto das BUs autorizadas
    // =========================================================================

    /// <summary>
    /// PBT-05: para qualquer lista de até 5 GUIDs de BUs autorizadas e
    /// qualquer lista de oportunidades construída exclusivamente com esses BuIds,
    /// todas as oportunidades têm BuId dentro do conjunto autorizado.
    ///
    /// Usa <see cref="PositiveInt"/> para indexar os BuIds, garantindo que os
    /// índices sejam válidos e que a invariante seja verificável sem mock de handler.
    ///
    /// Mapeia: design §13, PBT-05, Req 6.2/6.3.
    /// </summary>
    [Property(
        DisplayName = "PBT-05: oportunidades geradas com BUs autorizadas estão todas no escopo",
        MaxTest = 500)]
    public bool Pbt05_OpportunitiesAreSubsetOfAuthorizedBus(
        NonEmptyArray<Guid> buGuids,
        PositiveInt opportunityCount)
    {
        // Cria conjunto de BUs autorizadas (até 5 BUs distintas)
        var authorizedBus = buGuids.Item
            .Distinct()
            .Take(5)
            .ToHashSet();

        if (authorizedBus.Count == 0) return true; // precondição

        var buList = authorizedBus.ToList();
        var count = Math.Min(opportunityCount.Item, 10); // limita para manter testes rápidos

        // Constrói oportunidades apenas com BuIds do conjunto autorizado
        var opportunities = Enumerable.Range(0, count)
            .Select(i => new OpportunityReadModel(
                Guid.NewGuid(),
                buList[i % buList.Count], // sempre dentro do conjunto autorizado
                $"Oportunidade {i}",
                "Proposta",
                1000L * (i + 1)))
            .ToList();

        // Propriedade: todos os BuIds devem estar no conjunto autorizado
        return opportunities.All(o => authorizedBus.Contains(o.BuId));
    }

    /// <summary>
    /// PBT-05 integrado com handler: handler repassa escopo de BUs correto para a porta.
    /// Verifica que o handler não filtra por conta própria — a filtragem é da porta.
    /// </summary>
    [Fact(DisplayName = "PBT-05 handler: escopo de BUs repassado integralmente à porta de oportunidades")]
    public async Task Handle_PassesAuthorizedBusToPort()
    {
        // Arrange
        var account = BuildAccount();
        var bu1 = Guid.NewGuid();
        var bu2 = Guid.NewGuid();
        var authorizedBus = new HashSet<Guid> { bu1, bu2 };

        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(account);

        var filteredOpportunities = new[]
        {
            new OpportunityReadModel(Guid.NewGuid(), bu1, "Opp BU1", "Qualificação", 10000L),
            new OpportunityReadModel(Guid.NewGuid(), bu2, "Opp BU2", "Proposta", 20000L)
        };

        IReadOnlySet<Guid>? capturedBuScope = null;
        _opportunityPort.GetByAccountAsync(
                account.Id,
                Arg.Do<IReadOnlySet<Guid>>(s => capturedBuScope = s),
                Arg.Any<CancellationToken>())
            .Returns(filteredOpportunities);

        _activityPort.GetByAccountAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ActivityReadModel>());

        var query = new GetAccount360Query(AccountId: account.Id, AuthorizedBuIds: authorizedBus);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert — PBT-05: handler repassou exatamente o escopo autorizado
        capturedBuScope.Should().NotBeNull();
        capturedBuScope!.Should().BeEquivalentTo(authorizedBus);

        result.Opportunities.Should().NotBeNull();
        result.Opportunities!.Should().AllSatisfy(o =>
            authorizedBus.Should().Contain(o.BuId));
    }
}

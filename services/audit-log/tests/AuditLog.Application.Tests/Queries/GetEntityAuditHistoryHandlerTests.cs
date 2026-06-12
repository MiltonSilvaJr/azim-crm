using AuditLog.Application.Abstractions;
using AuditLog.Application.Handlers;
using AuditLog.Application.Queries;
using AuditLog.Domain.Abstractions;
using AuditLog.Domain.Aggregates;
using AuditLog.Domain.Repositories;
using AuditLog.Domain.Services;
using AuditLog.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AuditLog.Application.Tests.Queries;

/// <summary>
/// Testes unitários do <see cref="GetEntityAuditHistoryHandler"/>.
/// Verifica histórico correto por entidade, paginação e ausência de efeitos colaterais.
/// </summary>
public sealed class GetEntityAuditHistoryHandlerTests
{
    private readonly IAuditLogRepository _repository = Substitute.For<IAuditLogRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    private readonly Guid _tenantIdValue = Guid.NewGuid();
    private readonly Guid _entityId = Guid.NewGuid();

    public GetEntityAuditHistoryHandlerTests()
    {
        _tenantContext.TenantId.Returns(_tenantIdValue);
    }

    private GetEntityAuditHistoryHandler BuildSut() =>
        new(_repository, _tenantContext);

    // -----------------------------------------------------------------------
    // Comportamento básico
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Handler deve retornar histórico da entidade específica")]
    public async Task Handle_Should_Return_Entity_History()
    {
        var entry1 = BuildEntry(_entityId, DateTimeOffset.UtcNow.AddHours(-2));
        var entry2 = BuildEntry(_entityId, DateTimeOffset.UtcNow.AddHours(-1));

        _repository.FindByEntityAsync(
            Arg.Any<TenantId>(),
            Arg.Is<EntityReference>(e => e.EntityId == _entityId),
            Arg.Any<CancellationToken>())
            .Returns(new[] { entry2, entry1 }.ToList() as IReadOnlyList<AuditLogAggregate>);

        var query = new GetEntityAuditHistoryQuery
        {
            EntityType = "Opportunity",
            EntityId = _entityId,
            Page = 1,
            PageSize = 50
        };

        var sut = BuildSut();
        var result = await sut.Handle(query, CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    [Fact(DisplayName = "Handler deve ordenar por created_at desc")]
    public async Task Handle_Should_Order_By_CreatedAt_Desc()
    {
        var older = BuildEntry(_entityId, DateTimeOffset.UtcNow.AddHours(-3));
        var newer = BuildEntry(_entityId, DateTimeOffset.UtcNow.AddHours(-1));

        // Repositório retorna em ordem aleatória
        _repository.FindByEntityAsync(
            Arg.Any<TenantId>(),
            Arg.Any<EntityReference>(),
            Arg.Any<CancellationToken>())
            .Returns(new[] { older, newer }.ToList() as IReadOnlyList<AuditLogAggregate>);

        var query = new GetEntityAuditHistoryQuery
        {
            EntityType = "Opportunity",
            EntityId = _entityId
        };

        var sut = BuildSut();
        var result = await sut.Handle(query, CancellationToken.None);

        result.Items[0].CreatedAt.Should().BeAfter(result.Items[1].CreatedAt,
            because: "criado mais recente deve aparecer primeiro");
    }

    [Fact(DisplayName = "Handler não deve alterar estado (sem efeitos colaterais, REQ-007.5)")]
    public async Task Handle_Should_Not_Cause_Side_Effects()
    {
        _repository.FindByEntityAsync(
            Arg.Any<TenantId>(),
            Arg.Any<EntityReference>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<AuditLogAggregate>() as IReadOnlyList<AuditLogAggregate>);

        var sut = BuildSut();
        var query = new GetEntityAuditHistoryQuery
        {
            EntityType = "Opportunity",
            EntityId = _entityId
        };

        await sut.Handle(query, CancellationToken.None);
        await sut.Handle(query, CancellationToken.None);

        await _repository.DidNotReceive().AddAsync(
            Arg.Any<AuditLogAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handler deve aplicar filtro de período From")]
    public async Task Handle_Should_Apply_From_Filter()
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-1);
        var old = BuildEntry(_entityId, cutoff.AddDays(-1));
        var recent = BuildEntry(_entityId, cutoff.AddHours(1));

        _repository.FindByEntityAsync(
            Arg.Any<TenantId>(), Arg.Any<EntityReference>(), Arg.Any<CancellationToken>())
            .Returns(new[] { old, recent }.ToList() as IReadOnlyList<AuditLogAggregate>);

        var sut = BuildSut();
        var result = await sut.Handle(
            new GetEntityAuditHistoryQuery
            {
                EntityType = "Opportunity",
                EntityId = _entityId,
                From = cutoff
            },
            CancellationToken.None);

        result.Items.Should().HaveCount(1,
            because: "apenas o registro após cutoff deve ser retornado");
    }

    [Fact(DisplayName = "Handler deve aplicar filtro de período To")]
    public async Task Handle_Should_Apply_To_Filter()
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-1);
        var old = BuildEntry(_entityId, cutoff.AddDays(-1));
        var recent = BuildEntry(_entityId, cutoff.AddHours(1));

        _repository.FindByEntityAsync(
            Arg.Any<TenantId>(), Arg.Any<EntityReference>(), Arg.Any<CancellationToken>())
            .Returns(new[] { old, recent }.ToList() as IReadOnlyList<AuditLogAggregate>);

        var sut = BuildSut();
        var result = await sut.Handle(
            new GetEntityAuditHistoryQuery
            {
                EntityType = "Opportunity",
                EntityId = _entityId,
                To = cutoff
            },
            CancellationToken.None);

        result.Items.Should().HaveCount(1,
            because: "apenas o registro antes do cutoff deve ser retornado");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private AuditLogAggregate BuildEntry(Guid entityId, DateTimeOffset createdAt)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(createdAt);

        return AuditLogAggregate.Create(
            TenantId.From(_tenantIdValue),
            ActorId.From(Guid.NewGuid()),
            EntityReference.Create("Opportunity", entityId),
            AuditAction.Create,
            AuditDelta.ForCreate(
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Test" }),
            clock);
    }
}

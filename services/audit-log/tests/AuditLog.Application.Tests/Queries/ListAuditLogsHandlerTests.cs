using AuditLog.Application.Abstractions;
using AuditLog.Application.Handlers;
using AuditLog.Application.Queries;
using AuditLog.Domain.Abstractions;
using AuditLog.Domain.Aggregates;
using AuditLog.Domain.Repositories;
using AuditLog.Domain.Services;
using AuditLog.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AuditLog.Application.Tests.Queries;

/// <summary>
/// Testes unitários do <see cref="ListAuditLogsHandler"/>.
/// Verifica paginação, filtros e aplicação do escopo de BU para GestorBU (DD-008).
/// </summary>
public sealed class ListAuditLogsHandlerTests
{
    private readonly IAuditLogRepository _repository = Substitute.For<IAuditLogRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly IUserContext _userContext = Substitute.For<IUserContext>();
    private readonly IBuScopeResolver _buScopeResolver = Substitute.For<IBuScopeResolver>();

    private readonly Guid _tenantIdValue = Guid.NewGuid();

    public ListAuditLogsHandlerTests()
    {
        _tenantContext.TenantId.Returns(_tenantIdValue);
        _userContext.Role.Returns(AuditRoles.TenantAdmin); // default: TenantAdmin
        _buScopeResolver.ResolveAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlySet<Guid>?)null);
    }

    private ListAuditLogsHandler BuildSut() =>
        new(_repository, _tenantContext, _userContext, _buScopeResolver,
            NullLogger<ListAuditLogsHandler>.Instance);

    // -----------------------------------------------------------------------
    // Comportamento básico (TenantAdmin)
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Handler deve retornar página com itens do repositório")]
    public async Task Handle_Should_Return_Paged_Result_From_Repository()
    {
        var entry = BuildEntry();
        _repository.ListAsync(
            Arg.Any<TenantId>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(),
            Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((new[] { entry }.ToList() as IReadOnlyList<AuditLogAggregate>, 1));

        var query = new ListAuditLogsQuery { Page = 1, PageSize = 50 };
        var sut = BuildSut();

        var result = await sut.Handle(query, CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(50);
        result.TotalCount.Should().Be(1);
    }

    [Fact(DisplayName = "Handler não deve alterar estado da trilha (sem efeitos colaterais)")]
    public async Task Handle_Should_Not_Cause_Side_Effects()
    {
        _repository.ListAsync(
            Arg.Any<TenantId>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(),
            Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((new List<AuditLogAggregate>() as IReadOnlyList<AuditLogAggregate>, 0));

        var sut = BuildSut();

        // Executa a mesma query duas vezes
        await sut.Handle(new ListAuditLogsQuery(), CancellationToken.None);
        await sut.Handle(new ListAuditLogsQuery(), CancellationToken.None);

        // Repositório só deve ter sido chamado para leitura — sem AddAsync (REQ-007.5)
        await _repository.DidNotReceive().AddAsync(
            Arg.Any<AuditLogAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handler deve usar TenantId do contexto, não de parâmetro externo")]
    public async Task Handle_Should_Use_TenantId_From_Context()
    {
        _repository.ListAsync(
            Arg.Any<TenantId>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(),
            Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((new List<AuditLogAggregate>() as IReadOnlyList<AuditLogAggregate>, 0));

        TenantId? capturedTenantId = null;
        await _repository.ListAsync(
            Arg.Do<TenantId>(t => capturedTenantId = t),
            Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(),
            Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());

        var sut = BuildSut();
        await sut.Handle(new ListAuditLogsQuery(), CancellationToken.None);

        capturedTenantId.Should().NotBeNull();
        capturedTenantId!.Value.Should().Be(_tenantIdValue,
            because: "TenantId deve vir do contexto autenticado (REQ-005.3)");
    }

    // -----------------------------------------------------------------------
    // Escopo de BU para GestorBU (DD-008)
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "GestorBU: handler deve chamar IBuScopeResolver")]
    public async Task GestorBU_Should_Call_BuScopeResolver()
    {
        _userContext.Role.Returns(AuditRoles.GestorBU);
        _buScopeResolver.ResolveAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlySet<Guid>?)null);
        _repository.ListAsync(
            Arg.Any<TenantId>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(),
            Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((new List<AuditLogAggregate>() as IReadOnlyList<AuditLogAggregate>, 0));

        var sut = BuildSut();
        await sut.Handle(new ListAuditLogsQuery(), CancellationToken.None);

        await _buScopeResolver.Received(1).ResolveAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "GestorBU: entityId fora do escopo deve retornar lista vazia")]
    public async Task GestorBU_Should_Return_Empty_For_EntityId_Outside_Scope()
    {
        _userContext.Role.Returns(AuditRoles.GestorBU);
        var allowedId = Guid.NewGuid();
        var outsideId = Guid.NewGuid();

        _buScopeResolver.ResolveAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlySet<Guid>?)new HashSet<Guid> { allowedId });

        var sut = BuildSut();
        var result = await sut.Handle(
            new ListAuditLogsQuery { EntityId = outsideId },
            CancellationToken.None);

        result.Items.Should().BeEmpty(because: "entityId não está no escopo de BU do GestorBU");
        result.TotalCount.Should().Be(0);
    }

    [Fact(DisplayName = "GestorBU: entityId no escopo deve consultar repositório normalmente")]
    public async Task GestorBU_Should_Query_For_EntityId_In_Scope()
    {
        _userContext.Role.Returns(AuditRoles.GestorBU);
        var allowedId = Guid.NewGuid();
        var entry = BuildEntryForEntity(allowedId);

        _buScopeResolver.ResolveAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlySet<Guid>?)new HashSet<Guid> { allowedId });

        _repository.ListAsync(
            Arg.Any<TenantId>(), Arg.Any<string?>(), Arg.Is<Guid?>(g => g == allowedId), Arg.Any<Guid?>(),
            Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((new[] { entry }.ToList() as IReadOnlyList<AuditLogAggregate>, 1));

        var sut = BuildSut();
        var result = await sut.Handle(
            new ListAuditLogsQuery { EntityId = allowedId },
            CancellationToken.None);

        result.Items.Should().HaveCount(1);
    }

    [Fact(DisplayName = "TenantAdmin: não deve chamar IBuScopeResolver")]
    public async Task TenantAdmin_Should_Not_Call_BuScopeResolver()
    {
        _userContext.Role.Returns(AuditRoles.TenantAdmin);
        _repository.ListAsync(
            Arg.Any<TenantId>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(),
            Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((new List<AuditLogAggregate>() as IReadOnlyList<AuditLogAggregate>, 0));

        var sut = BuildSut();
        await sut.Handle(new ListAuditLogsQuery(), CancellationToken.None);

        await _buScopeResolver.DidNotReceive().ResolveAsync(Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private AuditLogAggregate BuildEntry() =>
        BuildEntryForEntity(Guid.NewGuid());

    private AuditLogAggregate BuildEntryForEntity(Guid entityId)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);

        var policy = Substitute.For<IPiiFieldPolicy>();
        policy.GetPiiFields(Arg.Any<string>())
              .Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase));

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

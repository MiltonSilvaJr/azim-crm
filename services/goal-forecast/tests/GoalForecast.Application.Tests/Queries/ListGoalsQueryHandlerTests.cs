using GoalForecast.Application.Ports;
using GoalForecast.Application.Queries;
using GoalForecast.Domain.Aggregates;
using GoalForecast.Domain.Authorization;
using GoalForecast.Domain.ValueObjects;

namespace GoalForecast.Application.Tests.Queries;

/// <summary>
/// Testes do <see cref="ListGoalsQueryHandler"/>.
/// Verifica filtro RBAC por papel, paginação e valorMeta em centavos inteiros.
/// Mapeia: TASK-11, Req 3, Req 12, RNF 2.
/// </summary>
public sealed class ListGoalsQueryHandlerTests
{
    private readonly IGoalRepository _repository;
    private readonly ListGoalsQueryHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BuId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid OutraBuId = Guid.NewGuid();

    public ListGoalsQueryHandlerTests()
    {
        _repository = Substitute.For<IGoalRepository>();
        _handler = new ListGoalsQueryHandler(_repository);
    }

    // --- Cenário 1: Vendedor só vê suas próprias metas ---

    [Fact]
    public async Task Handle_vendedor_deve_filtrar_por_proprio_owner_id()
    {
        // Arrange
        var vendedorPrincipal = new GoalPrincipal(TenantId, OwnerId, GoalRole.Vendedor, BuId);
        var query = new ListGoalsQuery { Principal = vendedorPrincipal };

        var goalsDoVendedor = new List<Goal>
        {
            CriarGoal(BuId, OwnerId, 2026, 6, 50_000L)
        };
        _repository.Query(
            Arg.Is<GoalQueryFilter>(f =>
                f.TenantId == TenantId &&
                f.OwnerId == OwnerId),
            default)
            .Returns(new PagedResult<Goal>(goalsDoVendedor, 1, 50, 1));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].OwnerId.Should().Be(OwnerId);
        result.Items[0].ValorMeta.Should().Be(50_000L);
    }

    // --- Cenário 2: Gestor só vê metas da sua BU ---

    [Fact]
    public async Task Handle_gestor_deve_filtrar_por_propria_bu()
    {
        // Arrange
        var gestorPrincipal = new GoalPrincipal(TenantId, Guid.NewGuid(), GoalRole.GestorDeBu, BuId);
        var query = new ListGoalsQuery { Principal = gestorPrincipal };

        _repository.Query(
            Arg.Is<GoalQueryFilter>(f =>
                f.TenantId == TenantId &&
                f.BuId == BuId),
            default)
            .Returns(new PagedResult<Goal>([], 1, 50, 0));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert: query enviada ao repositório usa BuId do gestor
        await _repository.Received(1).Query(
            Arg.Is<GoalQueryFilter>(f => f.BuId == BuId),
            Arg.Any<CancellationToken>());
    }

    // --- Cenário 3: Admin vê todas as BUs do tenant ---

    [Fact]
    public async Task Handle_admin_deve_ver_todas_as_bus_do_tenant()
    {
        // Arrange
        var adminPrincipal = new GoalPrincipal(TenantId, Guid.NewGuid(), GoalRole.TenantAdmin, null);
        var query = new ListGoalsQuery { Principal = adminPrincipal, BuId = null };

        _repository.Query(
            Arg.Is<GoalQueryFilter>(f =>
                f.TenantId == TenantId &&
                f.BuId == null),
            default)
            .Returns(new PagedResult<Goal>([], 1, 50, 0));

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert: sem filtro de BU aplicado
        await _repository.Received(1).Query(
            Arg.Is<GoalQueryFilter>(f => f.BuId == null),
            Arg.Any<CancellationToken>());
    }

    // --- Cenário 4: valorMeta retornado como centavos inteiros (long) ---

    [Fact]
    public async Task Handle_deve_retornar_valorMeta_em_centavos_inteiros()
    {
        // Arrange
        var adminPrincipal = new GoalPrincipal(TenantId, Guid.NewGuid(), GoalRole.TenantAdmin, null);
        var query = new ListGoalsQuery { Principal = adminPrincipal };

        var goals = new List<Goal>
        {
            CriarGoal(BuId, null, 2026, 6, 123_456_789L)
        };
        _repository.Query(Arg.Any<GoalQueryFilter>(), default)
            .Returns(new PagedResult<Goal>(goals, 1, 50, 1));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items[0].ValorMeta.Should().Be(123_456_789L);
    }

    // --- Cenário 5: paginação respeitada com pageSize máximo 200 ---

    [Fact]
    public async Task Handle_deve_limitar_pageSize_a_200()
    {
        // Arrange
        var adminPrincipal = new GoalPrincipal(TenantId, Guid.NewGuid(), GoalRole.TenantAdmin, null);
        var query = new ListGoalsQuery { Principal = adminPrincipal, PageSize = 999 };

        _repository.Query(Arg.Any<GoalQueryFilter>(), default)
            .Returns(new PagedResult<Goal>([], 1, 200, 0));

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert: pageSize limitado a 200
        await _repository.Received(1).Query(
            Arg.Is<GoalQueryFilter>(f => f.PageSize == 200),
            Arg.Any<CancellationToken>());
    }

    // --- Cenário 6: Executivo vê o tenant inteiro ---

    [Fact]
    public async Task Handle_executivo_deve_ver_tenant_inteiro()
    {
        // Arrange
        var executivoPrincipal = new GoalPrincipal(TenantId, Guid.NewGuid(), GoalRole.Executivo, null);
        var query = new ListGoalsQuery { Principal = executivoPrincipal };

        _repository.Query(Arg.Any<GoalQueryFilter>(), default)
            .Returns(new PagedResult<Goal>([], 1, 50, 0));

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert: sem filtro de owner forçado
        await _repository.Received(1).Query(
            Arg.Is<GoalQueryFilter>(f =>
                f.TenantId == TenantId &&
                f.OwnerId == null),
            Arg.Any<CancellationToken>());
    }

    // --- Helper ---

    private static Goal CriarGoal(Guid buId, Guid? ownerId, int year, int month, long cents)
    {
        var scope = ownerId.HasValue
            ? GoalScope.ForResponsavel(buId, ownerId.Value)
            : GoalScope.ForBu(buId);
        return Goal.Create(TenantId, scope, new GoalPeriod(year, month), Money.Of(cents));
    }
}

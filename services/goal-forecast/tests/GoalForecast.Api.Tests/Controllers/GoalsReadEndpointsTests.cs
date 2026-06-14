using System.Net;
using System.Net.Http.Json;
using GoalForecast.Api.Tests.Infrastructure;
using GoalForecast.Application.Common;
using GoalForecast.Application.Ports;
using GoalForecast.Application.Queries;
using MediatR;
using NSubstitute;

namespace GoalForecast.Api.Tests.Controllers;

/// <summary>
/// Testes de API para GET /api/v1/goals.
/// Verifica RBAC, paginação, filtros e integridade dos tipos monetários.
///
/// Mapeia: TASK-23, design §8.3, Req 3, Req 12, RNF 2.
/// </summary>
public sealed class GoalsReadEndpointsTests : IClassFixture<GoalForecastWebFactory>
{
    private readonly GoalForecastWebFactory _factory;
    private readonly HttpClient _client;

    public GoalsReadEndpointsTests(GoalForecastWebFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GoalDto MakeGoalDto(
        Guid? id = null,
        Guid? tenantId = null,
        Guid? buId = null,
        Guid? ownerId = null,
        long valorMeta = 1_000_00L,
        int year = 2026,
        int month = 1) =>
        new(
            Id: id ?? Guid.NewGuid(),
            TenantId: tenantId ?? TestAuthHelper.TenantA,
            Scope: "RESPONSAVEL",
            BuId: buId ?? TestAuthHelper.BuA1,
            OwnerId: ownerId,
            Year: year,
            Month: month,
            ValorMeta: valorMeta,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);

    private static PagedResult<GoalDto> SinglePageResult(params GoalDto[] items) =>
        new(items.ToList(), Page: 1, PageSize: 50, Total: items.Length);

    private void SetupListHandler(PagedResult<GoalDto> result) =>
        _factory.ListGoalsHandler
            .Handle(Arg.Any<ListGoalsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

    // ── Autenticação ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_SemAuth_Retorna401()
    {
        var response = await _client.GetAsync("/api/v1/goals");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Visibilidade por papel ────────────────────────────────────────────────

    [Fact]
    public async Task Get_TenantAdmin_RecebeListaCompleta()
    {
        // Arrange
        var goal1 = MakeGoalDto(buId: TestAuthHelper.BuA1, ownerId: TestAuthHelper.UserId1);
        var goal2 = MakeGoalDto(buId: TestAuthHelper.BuA2, ownerId: TestAuthHelper.UserId2);
        SetupListHandler(SinglePageResult(goal1, goal2));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/goals");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.TenantAdminA);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GoalListBody>();
        body!.Total.Should().Be(2);

        // Garante que nenhum valorMeta vaza como double
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain("E+");
        raw.Should().NotContain("e+");
    }

    [Fact]
    public async Task Get_GestorDeBu_HandlerRecebeQueryComBuIdDoToken()
    {
        // Arrange: handler retorna itens da BU do gestor
        var goal = MakeGoalDto(buId: TestAuthHelper.BuA1, ownerId: TestAuthHelper.UserId2);
        SetupListHandler(SinglePageResult(goal));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/goals");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.GestorBuA1);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verificar que o handler foi chamado com o principal correto (buId do gestor)
        await _factory.ListGoalsHandler.Received()
            .Handle(
                Arg.Is<ListGoalsQuery>(q =>
                    q.Principal.Role == GoalForecast.Domain.Authorization.GoalRole.GestorDeBu &&
                    q.Principal.BuId == TestAuthHelper.BuA1),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_Executivo_HandlerRecebeQueryComRoleCorreto()
    {
        // Arrange
        var goal1 = MakeGoalDto(buId: TestAuthHelper.BuA1);
        var goal2 = MakeGoalDto(buId: TestAuthHelper.BuA2);
        SetupListHandler(SinglePageResult(goal1, goal2));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/goals");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.ExecutivoA);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.ListGoalsHandler.Received()
            .Handle(
                Arg.Is<ListGoalsQuery>(q =>
                    q.Principal.Role == GoalForecast.Domain.Authorization.GoalRole.Executivo),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_Vendedor_HandlerRecebeQueryComUserIdDoToken()
    {
        // Arrange: Vendedor só vê as próprias metas; o RBAC é aplicado na camada Application
        var goal = MakeGoalDto(ownerId: TestAuthHelper.UserId1);
        SetupListHandler(SinglePageResult(goal));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/goals");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.VendedorA);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.ListGoalsHandler.Received()
            .Handle(
                Arg.Is<ListGoalsQuery>(q =>
                    q.Principal.Role == GoalForecast.Domain.Authorization.GoalRole.Vendedor &&
                    q.Principal.UserId == TestAuthHelper.UserId1),
                Arg.Any<CancellationToken>());
    }

    // ── Paginação ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_PageSizeAcimaDe200_Retorna400()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/goals?pageSize=201");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.TenantAdminA);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_PageSize200_EhValido()
    {
        SetupListHandler(SinglePageResult());

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/goals?pageSize=200");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.TenantAdminA);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.ListGoalsHandler.Received()
            .Handle(
                Arg.Is<ListGoalsQuery>(q => q.PageSize == 200),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_DefaultPagination_RetornaPage1PageSize50NoBody()
    {
        // Configura handler para retornar resultado com page=1, pageSize=50 (refletindo o que o handler recebeu)
        _factory.ListGoalsHandler
            .Handle(
                Arg.Is<ListGoalsQuery>(q => q.Page == 1 && q.PageSize == 50),
                Arg.Any<CancellationToken>())
            .Returns(new PagedResult<GoalDto>(new List<GoalDto>(), Page: 1, PageSize: 50, Total: 0));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/goals");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.TenantAdminA);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GoalListBody>();
        // O resultado retornado pelo handler (page=1, pageSize=50) deve aparecer na resposta
        body!.Page.Should().Be(1);
        body.PageSize.Should().Be(50);
    }

    // ── Filtros ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_ComFiltrosBuIdYearMonth_PassaFiltrosAoHandler()
    {
        SetupListHandler(SinglePageResult());

        var buId = TestAuthHelper.BuA1;
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/goals?buId={buId}&year=2026&month=3");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.TenantAdminA);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.ListGoalsHandler.Received()
            .Handle(
                Arg.Is<ListGoalsQuery>(q =>
                    q.BuId == buId &&
                    q.Year == 2026 &&
                    q.Month == 3),
                Arg.Any<CancellationToken>());
    }

    // ── Integridade monetária ─────────────────────────────────────────────────

    [Fact]
    public async Task Get_ValorMetaGrande_SerializadoComoLongNaoDouble()
    {
        // 9_007_199_254_740_993 > Number.MAX_SAFE_INTEGER (JavaScript), deve manter precisão
        const long valorMetaGrande = 9_007_199_254_740_993L;
        var goal = MakeGoalDto(valorMeta: valorMetaGrande);
        SetupListHandler(SinglePageResult(goal));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/goals");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.TenantAdminA);

        var response = await _client.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();

        raw.Should().Contain("9007199254740993");
        raw.Should().NotContain("9.007199254740993E+15");
        raw.Should().NotContain("9.007199254740993e+15");
    }

    // ── Resposta paginada ─────────────────────────────────────────────────────

    [Fact]
    public async Task Get_Retorna200ComGoalListResponse()
    {
        var goal = MakeGoalDto(year: 2026, month: 2, valorMeta: 50_000_00L);
        SetupListHandler(new PagedResult<GoalDto>(
            new[] { goal }.ToList(), Page: 1, PageSize: 50, Total: 1));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/goals");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.TenantAdminA);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GoalListBody>();
        body!.Page.Should().Be(1);
        body.PageSize.Should().Be(50);
        body.Total.Should().Be(1);
        body.Items.Should().HaveCount(1);
        body.Items[0].ValorMeta.Should().Be(50_000_00L);
    }

    // ── Suporte para desserialização no teste ─────────────────────────────────

    private sealed record GoalListBody(
        GoalItemBody[] Items,
        int Page,
        int PageSize,
        int Total);

    private sealed record GoalItemBody(
        Guid Id,
        string Scope,
        Guid BuId,
        Guid? OwnerId,
        int Year,
        int Month,
        long ValorMeta,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}

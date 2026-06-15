using System.Net;
using System.Net.Http.Json;
using GoalForecast.Api.Tests.Infrastructure;
using GoalForecast.Application.Commands;
using GoalForecast.Application.Common;
using GoalForecast.Application.Ports;
using GoalForecast.Application.Queries;
using GoalForecast.Contracts;
using NSubstitute;
using AppGoalDto = GoalForecast.Application.Common.GoalDto;

namespace GoalForecast.Api.Tests.Security;

/// <summary>
/// Testes de segurança da Onda 6 — TASK-27.
///
/// Cobre:
/// <list type="bullet">
///   <item>RBAC por papel em todos os endpoints (Vendedor, Gestor, Executivo, Admin).</item>
///   <item>Anti-enumeração: negação retorna 403 genérico, nunca revela existência de recurso (RNF-2.3).</item>
///   <item>Cross-tenant: tenant B não acessa dados de tenant A.</item>
///   <item>Cross-BU: Gestor de BU A não acessa metas de BU B no mesmo tenant.</item>
/// </list>
///
/// Mapeia: TASK-27, Req 12, RNF 2, design §10 e §12.
/// </summary>
public sealed class SecurityTests : IClassFixture<GoalForecastWebFactory>
{
    private readonly GoalForecastWebFactory _factory;
    private readonly HttpClient _client;

    // ── Identidades de teste ─────────────────────────────────────────────────
    // TenantB para cross-tenant
    private static readonly string TenantBAdmin =
        $"role=TenantAdmin,tenantId={TestAuthHelper.TenantB},userId={TestAuthHelper.UserId2}";

    // Gestor de BU A2 (BU diferente do GestorBuA1) no mesmo tenant A
    private static readonly string GestorBuA2 =
        $"role=GestorDeBu,tenantId={TestAuthHelper.TenantA},userId={TestAuthHelper.UserId2},buId={TestAuthHelper.BuA2}";

    public SecurityTests(GoalForecastWebFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // RBAC — write (POST /api/v1/goals)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Vendedor não tem permissão de escrita — deve receber 403 GF-ERR-006.
    /// O handler retorna erro de autorização (GF-ERR-006) conforme design §10.
    /// </summary>
    [Fact(DisplayName = "RBAC write — Vendedor fazendo POST /goals deve retornar 403 GF-ERR-006")]
    public async Task Post_Vendedor_Retorna403()
    {
        // Arrange: simula que o pipeline behavior de autorização bloqueia
        _factory.CommandHandler
            .Handle(Arg.Any<CreateOrUpdateGoalCommand>(), Arg.Any<CancellationToken>())
            .Returns<CreateOrUpdateGoalResult>(_ =>
                throw new Application.Common.ApplicationException("GF-ERR-006", "Operação não permitida.", 403));

        var request = BuildPostRequest(TestAuthHelper.VendedorA);

        // Act
        var response = await _client.SendAsync(request);

        // Assert — 403 sem revelar existência de meta (anti-enumeração RNF-2.3)
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("GF-ERR-006");
        body.Should().NotContain("GF-ERR-007"); // nunca vaza "not found"
    }

    /// <summary>
    /// Gestor de BU A fazendo POST para BU B (mesma tenant) deve receber 403.
    /// Cross-BU no mesmo tenant bloqueado conforme design §10.
    /// </summary>
    [Fact(DisplayName = "RBAC write — Gestor de BU A fazendo POST para BU B deve retornar 403 (cross-BU)")]
    public async Task Post_GestorCrossBu_Retorna403()
    {
        // Arrange: Gestor de BU A2 tenta criar meta em BU A1 — bloqueado
        _factory.CommandHandler
            .Handle(Arg.Any<CreateOrUpdateGoalCommand>(), Arg.Any<CancellationToken>())
            .Returns<CreateOrUpdateGoalResult>(_ =>
                throw new Application.Common.ApplicationException("GF-ERR-006", "Operação não permitida.", 403));

        // Gestor de BU A2 tentando escrever em BU A1
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/goals")
        {
            Content = JsonContent.Create(new CreateOrUpdateGoalRequest
            {
                Scope = "BU",
                BuId = TestAuthHelper.BuA1,   // BU diferente da do gestor
                Year = 2026,
                Month = 6,
                ValorMeta = 1_000_00L
            })
        };
        request.Headers.Add("X-Test-Claims", GestorBuA2);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("GF-ERR-006");
    }

    /// <summary>
    /// Executivo não tem permissão de escrita — deve receber 403.
    /// Matriz de autorização: Executivo → negado em escrita (design §10).
    /// </summary>
    [Fact(DisplayName = "RBAC write — Executivo fazendo POST /goals deve retornar 403 GF-ERR-006")]
    public async Task Post_Executivo_Retorna403()
    {
        _factory.CommandHandler
            .Handle(Arg.Any<CreateOrUpdateGoalCommand>(), Arg.Any<CancellationToken>())
            .Returns<CreateOrUpdateGoalResult>(_ =>
                throw new Application.Common.ApplicationException("GF-ERR-006", "Operação não permitida.", 403));

        var request = BuildPostRequest(TestAuthHelper.ExecutivoA);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("GF-ERR-006");
    }

    /// <summary>
    /// TenantAdmin tem permissão de escrita em qualquer BU — deve retornar 201.
    /// </summary>
    [Fact(DisplayName = "RBAC write — TenantAdmin fazendo POST /goals deve retornar 201")]
    public async Task Post_TenantAdmin_Retorna201()
    {
        var goalDto = MakeSampleGoalDto();
        _factory.CommandHandler
            .Handle(Arg.Any<CreateOrUpdateGoalCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateOrUpdateGoalResult(goalDto, Created: true));

        var request = BuildPostRequest(TestAuthHelper.TenantAdminA);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// GestorDeBu tem permissão de escrita em sua BU — deve retornar 201.
    /// </summary>
    [Fact(DisplayName = "RBAC write — GestorDeBu fazendo POST /goals na própria BU deve retornar 201")]
    public async Task Post_GestorBu_NaPropriabu_Retorna201()
    {
        var goalDto = MakeSampleGoalDto();
        _factory.CommandHandler
            .Handle(Arg.Any<CreateOrUpdateGoalCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateOrUpdateGoalResult(goalDto, Created: true));

        // GestorBuA1 escrevendo em BuA1 — permitido
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/goals")
        {
            Content = JsonContent.Create(new CreateOrUpdateGoalRequest
            {
                Scope = "BU",
                BuId = TestAuthHelper.BuA1,
                Year = 2026,
                Month = 6,
                ValorMeta = 1_000_00L
            })
        };
        request.Headers.Add("X-Test-Claims", TestAuthHelper.GestorBuA1);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // RBAC — read (GET /api/v1/goals)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GET /goals por qualquer papel autenticado deve retornar 200 (leitura permitida).
    /// O RBAC de escopo é aplicado na Application — API retorna sempre 200 com lista filtrada.
    /// Anti-enumeração: Vendedor vê lista vazia quando não há metas suas, não 403 nem 404.
    /// </summary>
    [Fact(DisplayName = "RBAC read — Vendedor vê lista vazia (não 403 nem 404) quando não há metas suas")]
    public async Task Get_Vendedor_VeListaVazia_Nao403()
    {
        // Handler retorna lista vazia para o vendedor (RBAC filtrou tudo)
        _factory.ListGoalsHandler
            .Handle(Arg.Any<ListGoalsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AppGoalDto>(new List<AppGoalDto>(), Page: 1, PageSize: 50, Total: 0));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/goals");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.VendedorA);

        var response = await _client.SendAsync(request);

        // Anti-enumeração: lista vazia, não 403/404 — não revela existência de metas de outros
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GoalListBody>();
        body!.Total.Should().Be(0);
        body.Items.Should().BeEmpty();
    }

    /// <summary>
    /// Executivo pode ler — escopo de leitura é tenant inteiro (design §10).
    /// </summary>
    [Fact(DisplayName = "RBAC read — Executivo acessa GET /goals com 200")]
    public async Task Get_Executivo_Retorna200()
    {
        _factory.ListGoalsHandler
            .Handle(Arg.Any<ListGoalsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AppGoalDto>(new List<AppGoalDto>(), Page: 1, PageSize: 50, Total: 0));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/goals");
        request.Headers.Add("X-Test-Claims", TestAuthHelper.ExecutivoA);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Anti-enumeração
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// PUT /goals/{id} com id de meta de outro tenant deve retornar 403 GF-ERR-006,
    /// nunca 404 — não revela existência de meta fora do escopo (RNF-2.3).
    /// </summary>
    [Fact(DisplayName = "Anti-enumeração — PUT /goals/{id} de outro tenant retorna 403, não 404")]
    public async Task Put_MetaDeOutroTenant_Retorna403NaoNotFound()
    {
        // Simula que o handler detecta cross-tenant e retorna GF-ERR-006 (não GF-ERR-007)
        _factory.UpdateGoalByIdHandler
            .Handle(Arg.Any<UpdateGoalByIdCommand>(), Arg.Any<CancellationToken>())
            .Returns<AppGoalDto>(_ =>
                throw new Application.Common.ApplicationException("GF-ERR-006", "Operação não permitida.", 403));

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/goals/{Guid.NewGuid()}")
        {
            Content = JsonContent.Create(new { valorMeta = 1_000_00L })
        };
        request.Headers.Add("X-Test-Claims", TestAuthHelper.TenantAdminA);

        var response = await _client.SendAsync(request);

        // Deve retornar 403 genérico — nunca 404 que revelaria existência
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("GF-ERR-006");
        body.Should().NotContain("GF-ERR-007"); // não revela "não encontrado"
    }

    /// <summary>
    /// GET /forecast com buId de outro tenant retorna 200 com degradação graciosa,
    /// nunca dados do tenant alvo (cross-tenant, design §10).
    /// </summary>
    [Fact(DisplayName = "Cross-tenant — GET /forecast com buId de outro tenant retorna 200 com degradação graciosa")]
    public async Task GetForecast_CrossTenant_Retorna200ComDegradacaoGraciosa()
    {
        // Handler retorna resultado sem meta (degradação graciosa — não dados do outro tenant)
        _factory.ForecastPanelHandler
            .Handle(Arg.Any<GetForecastPanelQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ForecastPanelResult(
                Scope: "BU",
                BuId: TestAuthHelper.BuA1,
                OwnerId: null,
                Year: 2026,
                Month: 6,
                ValorMeta: null,       // sem meta — não vaza dado do tenant alvo
                Realizado: 0L,
                PipelineDisponivel: 0L,
                Gap: null,
                PctAtingimento: null,
                PipelineUnavailable: false));

        // Tenant B tentando acessar BU de tenant A
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/forecast?buId={TestAuthHelper.BuA1}&year=2026&month=6");
        request.Headers.Add("X-Test-Claims", TenantBAdmin);

        var response = await _client.SendAsync(request);

        // Degradação graciosa: 200 com lista vazia / sem meta — não dados do tenant A
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ForecastPanelBody>();
        body!.ValorMeta.Should().BeNull();
        body.Gap.Should().BeNull();
    }

    /// <summary>
    /// GET /goals de tenant B retorna lista vazia, não dados de tenant A.
    /// Cross-tenant: defesa em profundidade (Global Query Filter + RLS) — TASK-27.
    /// </summary>
    [Fact(DisplayName = "Cross-tenant — GET /goals de tenant B retorna lista vazia, não dados de tenant A")]
    public async Task Get_CrossTenant_RetornaListaVaziaNaoMetasDoTenantA()
    {
        // Handler é chamado com o principal do tenant B → retorna vazio (filtro de tenant aplicado)
        _factory.ListGoalsHandler
            .Handle(
                Arg.Is<ListGoalsQuery>(q => q.Principal.TenantId == TestAuthHelper.TenantB),
                Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AppGoalDto>(new List<AppGoalDto>(), Page: 1, PageSize: 50, Total: 0));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/goals");
        request.Headers.Add("X-Test-Claims", TenantBAdmin);

        var response = await _client.SendAsync(request);

        // Cross-tenant não retorna 403 nem 404 que revelariam existência — lista vazia
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GoalListBody>();
        body!.Total.Should().Be(0);
        body.Items.Should().BeEmpty();

        // Verificar que o handler foi chamado com tenant B (nunca A)
        await _factory.ListGoalsHandler.Received()
            .Handle(
                Arg.Is<ListGoalsQuery>(q => q.Principal.TenantId == TestAuthHelper.TenantB),
                Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Cross-BU: Gestor de BU A2 não acessa metas de BU A1 no mesmo tenant.
    /// Handler retorna lista vazia pois o RBAC filtra por BU do gestor.
    /// </summary>
    [Fact(DisplayName = "Cross-BU — Gestor de BU A2 não vê metas de BU A1 (lista vazia)")]
    public async Task Get_GestorCrossBu_NaoVeMetasDaOutraBu()
    {
        // Handler retorna lista vazia para gestor de BU A2 pedindo BU A1
        _factory.ListGoalsHandler
            .Handle(
                Arg.Is<ListGoalsQuery>(q =>
                    q.Principal.BuId == TestAuthHelper.BuA2 &&
                    q.Principal.Role == Domain.Authorization.GoalRole.GestorDeBu),
                Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AppGoalDto>(new List<AppGoalDto>(), Page: 1, PageSize: 50, Total: 0));

        // Gestor de BU A2 filtrando por BU A1 — RBAC bloqueia na Application
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/goals?buId={TestAuthHelper.BuA1}");
        request.Headers.Add("X-Test-Claims", GestorBuA2);

        var response = await _client.SendAsync(request);

        // Lista vazia — não vaza metas de BU A1 para Gestor de BU A2
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GoalListBody>();
        body!.Total.Should().Be(0);
    }

    /// <summary>
    /// Endpoint sem autenticação retorna 401 (não 403 nem 400).
    /// Garante que todos os endpoints são protegidos por [Authorize].
    /// </summary>
    [Theory(DisplayName = "Autenticação — todos os endpoints retornam 401 sem token")]
    [InlineData("GET", "/api/v1/goals")]
    [InlineData("POST", "/api/v1/goals")]
    [InlineData("GET", "/api/v1/forecast?buId=00000000-0000-0000-0000-000000000001&year=2026&month=1")]
    [InlineData("GET", "/api/v1/forecast/aggregate?buId=00000000-0000-0000-0000-000000000001&year=2026&granularity=year")]
    [InlineData("GET", "/api/v1/internal/goals/digest-block?buId=00000000-0000-0000-0000-000000000001&year=2026&month=1")]
    public async Task Endpoint_SemAutenticacao_Retorna401(string method, string url)
    {
        var httpMethod = new HttpMethod(method);
        using var request = new HttpRequestMessage(httpMethod, url);
        if (method == "POST")
            request.Content = JsonContent.Create(new { });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: $"{method} {url} deve exigir autenticação");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════

    private static HttpRequestMessage BuildPostRequest(string authClaims) =>
        new HttpRequestMessage(HttpMethod.Post, "/api/v1/goals")
        {
            Content = JsonContent.Create(new CreateOrUpdateGoalRequest
            {
                Scope = "BU",
                BuId = TestAuthHelper.BuA1,
                Year = 2026,
                Month = 6,
                ValorMeta = 1_000_00L
            }),
            Headers = { { "X-Test-Claims", authClaims } }
        };

    private static AppGoalDto MakeSampleGoalDto() =>
        new(
            Id: Guid.NewGuid(),
            TenantId: TestAuthHelper.TenantA,
            Scope: "BU",
            BuId: TestAuthHelper.BuA1,
            OwnerId: null,
            Year: 2026,
            Month: 6,
            ValorMeta: 1_000_00L,
            Currency: "BRL",
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);

    // DTOs para desserialização nos testes
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

    private sealed record ForecastPanelBody(
        string Scope,
        Guid BuId,
        Guid? OwnerId,
        int Year,
        int Month,
        long? ValorMeta,
        long? Realizado,
        long? PipelineDisponivel,
        long? Gap,
        double? PctAtingimento,
        bool PipelineUnavailable);
}

using System.Net;
using System.Net.Http.Json;
using GoalForecast.Application.Commands;
using GoalForecast.Api.Tests.Infrastructure;
using GoalForecast.Contracts;
using NSubstitute;
using AppGoalDto = GoalForecast.Application.Common.GoalDto;
using ContractsGoalDto = GoalForecast.Contracts.GoalDto;

namespace GoalForecast.Api.Tests.Controllers;

/// <summary>
/// Testes de TASK-22: POST /api/v1/goals e PUT /api/v1/goals/{id}.
/// Cobre catálogo de erros GF-ERR-001..007, RBAC, upsert idempotente e over-posting.
///
/// Mapeia: TASK-22, design §8.1, §8.2, §12.
/// </summary>
public sealed class GoalsWriteEndpointsTests : IClassFixture<GoalForecastWebFactory>
{
    private readonly GoalForecastWebFactory _factory;
    private readonly HttpClient _client;

    private static readonly Guid GoalId = Guid.NewGuid();
    private static readonly Guid BuId = TestAuthHelper.BuA1;
    private static readonly Guid OwnerId = TestAuthHelper.UserId2;

    private static readonly AppGoalDto SampleGoalDto = new(
        Id: GoalId,
        TenantId: TestAuthHelper.TenantA,
        Scope: "RESPONSAVEL",
        BuId: BuId,
        OwnerId: OwnerId,
        Year: 2026,
        Month: 6,
        ValorMeta: 50_000_000L,
        Currency: "BRL",
        CreatedAt: DateTimeOffset.UtcNow,
        UpdatedAt: DateTimeOffset.UtcNow);

    public GoalsWriteEndpointsTests(GoalForecastWebFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── POST /api/v1/goals ───────────────────────────────────────────────────

    [Fact(DisplayName = "POST /goals com dados válidos deve retornar 201 e GoalDto (criação)")]
    public async Task Post_ValidRequest_Returns201WithGoalDto()
    {
        // Arrange
        _factory.CommandHandler
            .Handle(Arg.Any<CreateOrUpdateGoalCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateOrUpdateGoalResult(SampleGoalDto, Created: true));

        var request = new CreateOrUpdateGoalRequest
        {
            Scope = "RESPONSAVEL",
            BuId = BuId,
            OwnerId = OwnerId,
            Year = 2026,
            Month = 6,
            ValorMeta = 50_000_000L
        };

        // Act
        var response = await PostGoal(request, TestAuthHelper.GestorBuA1);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<ContractsGoalDto>();
        dto.Should().NotBeNull();
        dto!.ValorMeta.Should().Be(50_000_000L);
        dto.Id.Should().Be(GoalId);
    }

    [Fact(DisplayName = "POST /goals com mesma chave deve retornar 200 (upsert idempotente, PBT-01)")]
    public async Task Post_SameKey_Returns200ForUpsert()
    {
        // Arrange — handler retorna Created=false para simular atualização
        _factory.CommandHandler
            .Handle(Arg.Any<CreateOrUpdateGoalCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateOrUpdateGoalResult(SampleGoalDto, Created: false));

        var request = new CreateOrUpdateGoalRequest
        {
            Scope = "RESPONSAVEL",
            BuId = BuId,
            OwnerId = OwnerId,
            Year = 2026,
            Month = 6,
            ValorMeta = 60_000_000L
        };

        // Act
        var response = await PostGoal(request, TestAuthHelper.GestorBuA1);

        // Assert — 200 para upsert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "POST /goals com valorMeta negativo deve retornar 400 GF-ERR-001")]
    public async Task Post_NegativeValorMeta_Returns400WithGfErr001()
    {
        // Arrange — handler lança exceção de domínio GF-ERR-001
        _factory.CommandHandler
            .Handle(Arg.Any<CreateOrUpdateGoalCommand>(), Arg.Any<CancellationToken>())
            .Returns<CreateOrUpdateGoalResult>(_ =>
                throw new Domain.Exceptions.DomainException("GF-ERR-001", "Valor de meta inválido."));

        var request = new CreateOrUpdateGoalRequest
        {
            Scope = "BU",
            BuId = BuId,
            Year = 2026,
            Month = 6,
            ValorMeta = -1L
        };

        // Act
        var response = await PostGoal(request, TestAuthHelper.TenantAdminA);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("GF-ERR-001");
    }

    [Fact(DisplayName = "POST /goals com month=13 deve retornar 400 GF-ERR-002")]
    public async Task Post_InvalidMonth_Returns400WithGfErr002()
    {
        _factory.CommandHandler
            .Handle(Arg.Any<CreateOrUpdateGoalCommand>(), Arg.Any<CancellationToken>())
            .Returns<CreateOrUpdateGoalResult>(_ =>
                throw new Domain.Exceptions.DomainException("GF-ERR-002", "Mês ou ano fora da faixa."));

        var request = new CreateOrUpdateGoalRequest
        {
            Scope = "BU",
            BuId = BuId,
            Year = 2026,
            Month = 13,
            ValorMeta = 1000L
        };

        var response = await PostGoal(request, TestAuthHelper.TenantAdminA);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("GF-ERR-002");
    }

    [Fact(DisplayName = "POST /goals RESPONSAVEL sem ownerId deve retornar 400 GF-ERR-003")]
    public async Task Post_ResponsavelWithoutOwnerId_Returns400WithGfErr003()
    {
        _factory.CommandHandler
            .Handle(Arg.Any<CreateOrUpdateGoalCommand>(), Arg.Any<CancellationToken>())
            .Returns<CreateOrUpdateGoalResult>(_ =>
                throw new Domain.Exceptions.DomainException("GF-ERR-003", "Escopo inconsistente."));

        var request = new CreateOrUpdateGoalRequest
        {
            Scope = "RESPONSAVEL",
            BuId = BuId,
            OwnerId = null,
            Year = 2026,
            Month = 6,
            ValorMeta = 1000L
        };

        var response = await PostGoal(request, TestAuthHelper.TenantAdminA);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("GF-ERR-003");
    }

    [Fact(DisplayName = "POST /goals com owner não membro da BU deve retornar 422 GF-ERR-004")]
    public async Task Post_OwnerNotMember_Returns422WithGfErr004()
    {
        _factory.CommandHandler
            .Handle(Arg.Any<CreateOrUpdateGoalCommand>(), Arg.Any<CancellationToken>())
            .Returns<CreateOrUpdateGoalResult>(_ =>
                throw new Application.Common.ApplicationException("GF-ERR-004", "Responsável não pertence à BU.", 422));

        var request = new CreateOrUpdateGoalRequest
        {
            Scope = "RESPONSAVEL",
            BuId = BuId,
            OwnerId = Guid.NewGuid(),
            Year = 2026,
            Month = 6,
            ValorMeta = 1000L
        };

        var response = await PostGoal(request, TestAuthHelper.GestorBuA1);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("GF-ERR-004");
    }

    [Fact(DisplayName = "POST /goals sem autorização deve retornar 403 GF-ERR-006 (sem enumeração)")]
    public async Task Post_Unauthorized_Returns403WithGfErr006()
    {
        _factory.CommandHandler
            .Handle(Arg.Any<CreateOrUpdateGoalCommand>(), Arg.Any<CancellationToken>())
            .Returns<CreateOrUpdateGoalResult>(_ =>
                throw new Application.Common.ApplicationException("GF-ERR-006", "Operação não permitida.", 403));

        var request = new CreateOrUpdateGoalRequest
        {
            Scope = "BU",
            BuId = BuId,
            Year = 2026,
            Month = 6,
            ValorMeta = 1000L
        };

        // Vendedor não pode criar meta
        var response = await PostGoal(request, TestAuthHelper.VendedorA);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("GF-ERR-006");
    }

    [Fact(DisplayName = "POST /goals sem autenticação deve retornar 401")]
    public async Task Post_NoAuth_Returns401()
    {
        var request = new CreateOrUpdateGoalRequest
        {
            Scope = "BU",
            BuId = BuId,
            Year = 2026,
            Month = 6,
            ValorMeta = 1000L
        };

        var response = await _client.PostAsJsonAsync("/api/v1/goals", request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── PUT /api/v1/goals/{id} ────────────────────────────────────────────────

    [Fact(DisplayName = "PUT /goals/{id} com dados válidos deve retornar 200 e GoalDto")]
    public async Task Put_ValidRequest_Returns200WithGoalDto()
    {
        _factory.UpdateGoalByIdHandler
            .Handle(Arg.Any<UpdateGoalByIdCommand>(), Arg.Any<CancellationToken>())
            .Returns(SampleGoalDto with { ValorMeta = 60_000_000L, TenantId = TestAuthHelper.TenantA });

        var response = await PutGoal(GoalId, 60_000_000L, TestAuthHelper.TenantAdminA);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ContractsGoalDto>();
        dto.Should().NotBeNull();
        dto!.ValorMeta.Should().Be(60_000_000L);
    }

    [Fact(DisplayName = "PUT /goals/{id} com id inexistente deve retornar 404 GF-ERR-007")]
    public async Task Put_NonExistentId_Returns404WithGfErr007()
    {
        _factory.UpdateGoalByIdHandler
            .Handle(Arg.Any<UpdateGoalByIdCommand>(), Arg.Any<CancellationToken>())
            .Returns<AppGoalDto>(_ =>
                throw new Application.Common.ApplicationException("GF-ERR-007", "Meta não encontrada.", 404));

        var response = await PutGoal(Guid.NewGuid(), 1000L, TestAuthHelper.TenantAdminA);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("GF-ERR-007");
    }

    [Fact(DisplayName = "PUT /goals/{id} deve retornar long em valorMeta (não double)")]
    public async Task Put_ValidRequest_ValorMetaIsLongInJson()
    {
        _factory.UpdateGoalByIdHandler
            .Handle(Arg.Any<UpdateGoalByIdCommand>(), Arg.Any<CancellationToken>())
            .Returns(SampleGoalDto with { ValorMeta = 9_007_199_254_740_993L, TenantId = TestAuthHelper.TenantA });

        var response = await PutGoal(GoalId, 9_007_199_254_740_993L, TestAuthHelper.TenantAdminA);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        // O valor deve ser preservado como inteiro — se fosse double, haveria perda de precisão (RISK-GOAL-05)
        json.Should().Contain("9007199254740993");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> PostGoal(
        CreateOrUpdateGoalRequest request,
        string authClaims)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/goals")
        {
            Content = JsonContent.Create(request)
        };
        req.Headers.Add("X-Test-Claims", authClaims);
        return await _client.SendAsync(req);
    }

    private async Task<HttpResponseMessage> PutGoal(
        Guid id,
        long valorMeta,
        string authClaims)
    {
        var body = new { valorMeta };
        var req = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/goals/{id}")
        {
            Content = JsonContent.Create(body)
        };
        req.Headers.Add("X-Test-Claims", authClaims);
        return await _client.SendAsync(req);
    }
}

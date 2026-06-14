namespace ActivityManagement.Api.Tests.Controllers;

using System.Net;
using System.Net.Http.Json;
using ActivityManagement.Api.Tests.Helpers;
using ActivityManagement.Application.Activities.Commands;
using ActivityManagement.Application.Activities.Queries;
using ActivityManagement.Application.Behaviors;
using ActivityManagement.Contracts.Activities;
using MediatR;
using NSubstitute;
using Xunit;

/// <summary>
/// Testes de contrato do ActivitiesController (TASK-18).
/// Verifica códigos HTTP, formato de resposta, RBAC e headers de correlação.
/// Usa WebApplicationFactory com autenticação de teste e MediatR mockado.
/// </summary>
public sealed class ActivitiesControllerTests : IDisposable
{
    private readonly ActivityManagementWebFactory _factory;
    private readonly IMediator _mediator;
    private readonly Guid _tenantId = Guid.NewGuid();

    public ActivitiesControllerTests()
    {
        _mediator = Substitute.For<IMediator>();
        _factory  = new ActivityManagementWebFactory()
            .WithClaims(TestJwtHelper.SellerClaims(_tenantId))
            .WithSubstitute(_mediator);
    }

    public void Dispose() => _factory.Dispose();

    // ── GET /api/v1/activities ────────────────────────────────────────────────

    [Fact]
    public async Task GetActivities_RetornaOk_ComListaVazia()
    {
        // Arrange
        _mediator
            .Send(Arg.Any<ListActivitiesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ListActivitiesResult([], 0, 1, 20));

        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/activities");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedActivitiesResponse>();
        Assert.NotNull(result);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetActivities_SemAutenticacao_Retorna401()
    {
        // Factory sem claims → TestAuthHandler retorna Fail → [Authorize] retorna 401
        using var unauthFactory = new ActivityManagementWebFactory()
            .WithClaims([]) // lista vazia → sem autenticação
            .WithSubstitute(_mediator);
        var client = unauthFactory.CreateClient();
        var response = await client.GetAsync("/api/v1/activities");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── POST /api/v1/activities ───────────────────────────────────────────────

    [Fact]
    public async Task CreateActivity_Valido_Retorna201()
    {
        // Arrange
        var newId = Guid.NewGuid();
        _mediator
            .Send(Arg.Any<CreateActivityCommand>(), Arg.Any<CancellationToken>())
            .Returns(newId);

        var client = _factory.CreateAuthenticatedClient();
        var request = new CreateActivityRequest(
            Type:   "meeting",
            Title:  "Reunião de alinhamento",
            DueAt:  DateTimeOffset.UtcNow.AddDays(1),
            OwnerId: Guid.NewGuid());

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/activities", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("/api/v1/activities/", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task CreateActivity_Viewer_Retorna403()
    {
        // Mediator simula AuthorizationBehavior recusando o papel viewer (ACT-ERR-007)
        var viewerMediator = Substitute.For<IMediator>();
        viewerMediator
            .Send(Arg.Any<CreateActivityCommand>(), Arg.Any<CancellationToken>())
            .Returns<Guid>(_ => throw new InsufficientScopeException("viewer"));

        var factory = new ActivityManagementWebFactory()
            .WithClaims(TestJwtHelper.ViewerClaims(_tenantId))
            .WithSubstitute(viewerMediator);
        var client = factory.CreateAuthenticatedClient();

        var request = new CreateActivityRequest("meeting", "Título", DateTimeOffset.UtcNow.AddDays(1), Guid.NewGuid());
        var response = await client.PostAsJsonAsync("/api/v1/activities", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("ACT-ERR-007", error.Code);
        Assert.NotEqual(Guid.Empty, error.CorrelationId);

        factory.Dispose();
    }

    // ── GET /api/v1/activities/{id} ───────────────────────────────────────────

    [Fact]
    public async Task GetActivity_Existente_RetornaOk()
    {
        // Arrange
        var actId    = Guid.NewGuid();
        var response = BuildActivityResponse(actId);
        _mediator
            .Send(Arg.Any<GetActivityByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(response);

        var client = _factory.CreateAuthenticatedClient();

        // Act
        var httpResponse = await client.GetAsync($"/api/v1/activities/{actId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        var result = await httpResponse.Content.ReadFromJsonAsync<ActivityResponse>();
        Assert.NotNull(result);
        Assert.Equal(actId, result.Id);
    }

    [Fact]
    public async Task GetActivity_NaoEncontrado_Retorna404ComCodigo()
    {
        // Arrange
        _mediator
            .Send(Arg.Any<GetActivityByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ActivityResponse>(new ActivityNotFoundException(Guid.NewGuid())));

        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync($"/api/v1/activities/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("ACT-ERR-003", error.Code);
    }

    // ── PATCH /api/v1/activities/{id}/complete ────────────────────────────────

    [Fact]
    public async Task Complete_Valido_Retorna200ComCompletedAt()
    {
        // Arrange
        var actId     = Guid.NewGuid();
        var completedAt = DateTimeOffset.UtcNow;
        _mediator
            .Send(Arg.Any<CompleteActivityCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CompleteActivityResult(actId, completedAt, false));

        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PatchAsync($"/api/v1/activities/{actId}/complete", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CompleteActivityResponse>();
        Assert.NotNull(result);
        Assert.Equal(actId, result.ActivityId);
        Assert.False(result.WasAlreadyCompleted);
    }

    [Fact]
    public async Task Complete_JaCompletada_Retorna200Idempotente()
    {
        // Arrange
        var actId       = Guid.NewGuid();
        var completedAt = DateTimeOffset.UtcNow.AddHours(-1);
        _mediator
            .Send(Arg.Any<CompleteActivityCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CompleteActivityResult(actId, completedAt, WasAlreadyCompleted: true));

        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PatchAsync($"/api/v1/activities/{actId}/complete", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CompleteActivityResponse>();
        Assert.NotNull(result);
        Assert.True(result.WasAlreadyCompleted);
    }

    // ── GET /api/v1/activities/me/day ─────────────────────────────────────────

    [Fact]
    public async Task GetMyDay_RetornaOk_ComTresFaixas()
    {
        // Arrange
        _mediator
            .Send(Arg.Any<GetMyDayQuery>(), Arg.Any<CancellationToken>())
            .Returns(new MyDayResult([], [], []));

        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/activities/me/day");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MyDayResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Overdue);
        Assert.NotNull(result.Today);
        Assert.NotNull(result.Upcoming);
    }

    // ── Headers de correlação ─────────────────────────────────────────────────

    [Fact]
    public async Task TodosEndpoints_RespostasDeErro_ContemCorrelationId()
    {
        // Arrange
        _mediator
            .Send(Arg.Any<GetActivityByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ActivityResponse>(new ActivityNotFoundException(Guid.NewGuid())));

        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync($"/api/v1/activities/{Guid.NewGuid()}");

        // Assert
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.NotEqual(Guid.Empty, error.CorrelationId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ActivityResponse BuildActivityResponse(Guid id) => new(
        Id:            id,
        Type:          "meeting",
        Title:         "Reunião",
        Description:   null,
        DueAt:         DateTimeOffset.UtcNow.AddDays(1),
        Status:        "pending",
        Priority:      "medium",
        OwnerId:       Guid.NewGuid(),
        BuId:          Guid.NewGuid(),
        TenantId:      Guid.NewGuid(),
        OpportunityId: null,
        AccountId:     null,
        CompletedAt:   null,
        CreatedAt:     DateTimeOffset.UtcNow.AddDays(-1),
        UpdatedAt:     DateTimeOffset.UtcNow);
}

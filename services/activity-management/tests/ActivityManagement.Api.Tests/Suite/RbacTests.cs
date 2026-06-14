namespace ActivityManagement.Api.Tests.Suite;

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
/// Suite de testes de RBAC (TASK-21).
/// Verifica que cada papel (viewer, seller, bu_manager) tem os acessos corretos
/// conforme Req 13 e design §10. Usa WebApplicationFactory com mediator mock.
/// </summary>
public sealed class RbacTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();

    public void Dispose() { }

    // ── Viewer não pode criar atividade → 403 ACT-ERR-007 ────────────────────

    [Fact]
    public async Task Viewer_NaoPodeCriarAtividade_Retorna403()
    {
        var mediator = Substitute.For<IMediator>();
        mediator
            .Send(Arg.Any<CreateActivityCommand>(), Arg.Any<CancellationToken>())
            .Returns<Guid>(_ => throw new InsufficientScopeException("viewer"));

        using var factory = new ActivityManagementWebFactory()
            .WithClaims(TestJwtHelper.ViewerClaims(_tenantId))
            .WithSubstitute(mediator);
        var client = factory.CreateAuthenticatedClient();

        var request = new CreateActivityRequest("meeting", "Título", DateTimeOffset.UtcNow.AddDays(1), Guid.NewGuid());
        var response = await client.PostAsJsonAsync("/api/v1/activities", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("ACT-ERR-007", error.Code);
    }

    // ── Viewer não pode atualizar atividade → 403 ACT-ERR-007 ─────────────────

    [Fact]
    public async Task Viewer_NaoPodeAtualizarAtividade_Retorna403()
    {
        var mediator = Substitute.For<IMediator>();
        mediator
            .Send(Arg.Any<UpdateActivityCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new InsufficientScopeException("viewer"));

        using var factory = new ActivityManagementWebFactory()
            .WithClaims(TestJwtHelper.ViewerClaims(_tenantId))
            .WithSubstitute(mediator);
        var client = factory.CreateAuthenticatedClient();

        var request = new UpdateActivityRequest("meeting", "Novo título", DateTimeOffset.UtcNow.AddDays(2));
        var response = await client.PutAsJsonAsync($"/api/v1/activities/{Guid.NewGuid()}", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("ACT-ERR-007", error.Code);
    }

    // ── Viewer não pode excluir atividade → 403 ACT-ERR-007 ──────────────────

    [Fact]
    public async Task Viewer_NaoPodeExcluirAtividade_Retorna403()
    {
        var mediator = Substitute.For<IMediator>();
        mediator
            .Send(Arg.Any<DeleteActivityCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new InsufficientScopeException("viewer"));

        using var factory = new ActivityManagementWebFactory()
            .WithClaims(TestJwtHelper.ViewerClaims(_tenantId))
            .WithSubstitute(mediator);
        var client = factory.CreateAuthenticatedClient();

        var response = await client.DeleteAsync($"/api/v1/activities/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Viewer pode ler atividade → 200 ──────────────────────────────────────

    [Fact]
    public async Task Viewer_PodeLerAtividade_Retorna200()
    {
        var actId    = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator
            .Send(Arg.Any<GetActivityByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(BuildActivityResponse(actId, _tenantId));

        using var factory = new ActivityManagementWebFactory()
            .WithClaims(TestJwtHelper.ViewerClaims(_tenantId))
            .WithSubstitute(mediator);
        var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync($"/api/v1/activities/{actId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Seller pode criar atividade → 201 ────────────────────────────────────

    [Fact]
    public async Task Seller_PodeCriarAtividade_Retorna201()
    {
        var newId    = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator
            .Send(Arg.Any<CreateActivityCommand>(), Arg.Any<CancellationToken>())
            .Returns(newId);

        using var factory = new ActivityManagementWebFactory()
            .WithClaims(TestJwtHelper.SellerClaims(_tenantId))
            .WithSubstitute(mediator);
        var client = factory.CreateAuthenticatedClient();

        var request = new CreateActivityRequest("meeting", "Título", DateTimeOffset.UtcNow.AddDays(1), Guid.NewGuid());
        var response = await client.PostAsJsonAsync("/api/v1/activities", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ── BuManager pode criar atividade → 201 ─────────────────────────────────

    [Fact]
    public async Task BuManager_PodeCriarAtividade_Retorna201()
    {
        var newId    = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator
            .Send(Arg.Any<CreateActivityCommand>(), Arg.Any<CancellationToken>())
            .Returns(newId);

        using var factory = new ActivityManagementWebFactory()
            .WithClaims(TestJwtHelper.BuManagerClaims(_tenantId))
            .WithSubstitute(mediator);
        var client = factory.CreateAuthenticatedClient();

        var request = new CreateActivityRequest("meeting", "Título", DateTimeOffset.UtcNow.AddDays(1), Guid.NewGuid());
        var response = await client.PostAsJsonAsync("/api/v1/activities", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ActivityResponse BuildActivityResponse(Guid id, Guid tenantId) => new(
        Id:            id,
        Type:          "meeting",
        Title:         "Test",
        Description:   null,
        DueAt:         DateTimeOffset.UtcNow.AddDays(1),
        Status:        "pending",
        Priority:      "medium",
        OwnerId:       Guid.NewGuid(),
        BuId:          Guid.NewGuid(),
        TenantId:      tenantId,
        OpportunityId: null,
        AccountId:     null,
        CompletedAt:   null,
        CreatedAt:     DateTimeOffset.UtcNow,
        UpdatedAt:     DateTimeOffset.UtcNow);
}

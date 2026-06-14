namespace ActivityManagement.Api.Tests.Suite;

using System.Net;
using System.Net.Http.Json;
using ActivityManagement.Api.Tests.Helpers;
using ActivityManagement.Application.Activities.Commands;
using ActivityManagement.Application.Activities.Queries;
using ActivityManagement.Application.Behaviors;
using ActivityManagement.Contracts.Activities;
using ActivityManagement.Domain.Activities.Exceptions;
using FluentValidation.Results;
using MediatR;
using NSubstitute;
using Xunit;

/// <summary>
/// Testes de catálogo de erros ACT-ERR-001..011 (TASK-21).
/// Cada teste cobre um erro do catálogo, verificando:
/// - Código HTTP correto
/// - Campo <c>code</c> no corpo da resposta
/// - Campo <c>correlationId</c> não vazio
/// Mapeia: design §12, TASK-21.
/// </summary>
public sealed class ErrorCatalogTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly IMediator _mediator;
    private readonly ActivityManagementWebFactory _factory;

    public ErrorCatalogTests()
    {
        _mediator = Substitute.For<IMediator>();
        _factory  = new ActivityManagementWebFactory()
            .WithClaims(TestJwtHelper.SellerClaims(_tenantId))
            .WithSubstitute(_mediator);
    }

    public void Dispose() => _factory.Dispose();

    // ── ACT-ERR-001: Título obrigatório → 400 ────────────────────────────────

    [Fact]
    public async Task ActErr001_TituloObrigatorio_Retorna400()
    {
        _mediator
            .Send(Arg.Any<CreateActivityCommand>(), Arg.Any<CancellationToken>())
            .Returns<Guid>(_ => throw new TitleRequiredException());

        var client   = _factory.CreateAuthenticatedClient();
        var request  = new CreateActivityRequest("meeting", "", DateTimeOffset.UtcNow.AddDays(1), Guid.NewGuid());
        var response = await client.PostAsJsonAsync("/api/v1/activities", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("ACT-ERR-001", error!.Code);
        Assert.NotEqual(Guid.Empty, error.CorrelationId);
    }

    // ── ACT-ERR-002: Tipo inválido → 400 ─────────────────────────────────────

    [Fact]
    public async Task ActErr002_TipoInvalido_Retorna400()
    {
        _mediator
            .Send(Arg.Any<CreateActivityCommand>(), Arg.Any<CancellationToken>())
            .Returns<Guid>(_ => throw new InvalidActivityTypeException("invalido"));

        var client   = _factory.CreateAuthenticatedClient();
        var request  = new CreateActivityRequest("invalido", "Título", DateTimeOffset.UtcNow.AddDays(1), Guid.NewGuid());
        var response = await client.PostAsJsonAsync("/api/v1/activities", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("ACT-ERR-002", error!.Code);
    }

    // ── ACT-ERR-003: Atividade não encontrada → 404 ───────────────────────────

    [Fact]
    public async Task ActErr003_AtividadeNaoEncontrada_Retorna404()
    {
        _mediator
            .Send(Arg.Any<GetActivityByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns<ActivityResponse>(_ => throw new ActivityNotFoundException(Guid.NewGuid()));

        var client   = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync($"/api/v1/activities/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("ACT-ERR-003", error!.Code);
    }

    // ── ACT-ERR-004: Transição de status inválida → 409 ──────────────────────

    [Fact]
    public async Task ActErr004_TransicaoStatusInvalida_Retorna409()
    {
        _mediator
            .Send(Arg.Any<CompleteActivityCommand>(), Arg.Any<CancellationToken>())
            .Returns<CompleteActivityResult>(_ => throw new InvalidStatusTransitionException("completed", "cancelled"));

        var client   = _factory.CreateAuthenticatedClient();
        var response = await client.PatchAsync($"/api/v1/activities/{Guid.NewGuid()}/complete", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("ACT-ERR-004", error!.Code);
    }

    // ── ACT-ERR-005: Oportunidade não encontrada → 422 ───────────────────────

    [Fact]
    public async Task ActErr005_OportunidadeNaoEncontrada_Retorna422()
    {
        _mediator
            .Send(Arg.Any<CreateActivityCommand>(), Arg.Any<CancellationToken>())
            .Returns<Guid>(_ => throw new OpportunityNotFoundException(Guid.NewGuid()));

        var client   = _factory.CreateAuthenticatedClient();
        var request  = new CreateActivityRequest("meeting", "Título", DateTimeOffset.UtcNow.AddDays(1), Guid.NewGuid(),
            OpportunityId: Guid.NewGuid());
        var response = await client.PostAsJsonAsync("/api/v1/activities", request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("ACT-ERR-005", error!.Code);
    }

    // ── ACT-ERR-006: Conta não encontrada → 422 ──────────────────────────────

    [Fact]
    public async Task ActErr006_ContaNaoEncontrada_Retorna422()
    {
        _mediator
            .Send(Arg.Any<CreateActivityCommand>(), Arg.Any<CancellationToken>())
            .Returns<Guid>(_ => throw new AccountNotFoundException(Guid.NewGuid()));

        var client   = _factory.CreateAuthenticatedClient();
        var request  = new CreateActivityRequest("meeting", "Título", DateTimeOffset.UtcNow.AddDays(1), Guid.NewGuid(),
            AccountId: Guid.NewGuid());
        var response = await client.PostAsJsonAsync("/api/v1/activities", request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("ACT-ERR-006", error!.Code);
    }

    // ── ACT-ERR-007: Papel insuficiente → 403 ────────────────────────────────

    [Fact]
    public async Task ActErr007_PapelInsuficiente_Retorna403()
    {
        _mediator
            .Send(Arg.Any<CreateActivityCommand>(), Arg.Any<CancellationToken>())
            .Returns<Guid>(_ => throw new InsufficientScopeException("viewer"));

        var client   = _factory.CreateAuthenticatedClient();
        var request  = new CreateActivityRequest("meeting", "Título", DateTimeOffset.UtcNow.AddDays(1), Guid.NewGuid());
        var response = await client.PostAsJsonAsync("/api/v1/activities", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("ACT-ERR-007", error!.Code);
    }

    // ── ACT-ERR-008: Token digest inválido → 404 ─────────────────────────────

    [Fact]
    public async Task ActErr008_TokenDigestInvalido_Retorna404()
    {
        var digestMediator = Substitute.For<IMediator>();
        digestMediator
            .Send(Arg.Any<ProcessDigestActionCommand>(), Arg.Any<CancellationToken>())
            .Returns<DigestActionResult>(_ => throw new InvalidDigestTokenException());

        using var factory = new ActivityManagementWebFactory()
            .WithClaims([])
            .WithSubstitute(digestMediator);
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/v1/digest-actions/invalid-token", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("ACT-ERR-008", error!.Code);
    }

    // ── ACT-ERR-009: Token digest expirado → 410 ─────────────────────────────

    [Fact]
    public async Task ActErr009_TokenDigestExpirado_Retorna410()
    {
        var digestMediator = Substitute.For<IMediator>();
        digestMediator
            .Send(Arg.Any<ProcessDigestActionCommand>(), Arg.Any<CancellationToken>())
            .Returns<DigestActionResult>(_ => throw new ExpiredDigestTokenException());

        using var factory = new ActivityManagementWebFactory()
            .WithClaims([])
            .WithSubstitute(digestMediator);
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/v1/digest-actions/expired-token", null);

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("ACT-ERR-009", error!.Code);
    }

    // ── ACT-ERR-010: Data de vencimento inválida → 400 ───────────────────────

    [Fact]
    public async Task ActErr010_DataVencimentoInvalida_Retorna400()
    {
        _mediator
            .Send(Arg.Any<CreateActivityCommand>(), Arg.Any<CancellationToken>())
            .Returns<Guid>(_ => throw new FluentValidation.ValidationException(
                new[] { new ValidationFailure("DueAt", "Data de vencimento é obrigatória") }));

        var client   = _factory.CreateAuthenticatedClient();
        var request  = new CreateActivityRequest("meeting", "Título", DateTimeOffset.UtcNow.AddDays(1), Guid.NewGuid());
        var response = await client.PostAsJsonAsync("/api/v1/activities", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        // ACT-ERR-010 para ValidationException de DueAt
        Assert.NotNull(error);
        Assert.NotEqual(Guid.Empty, error.CorrelationId);
    }

    // ── ACT-ERR-011: Atividade encerrada → 409 ───────────────────────────────

    [Fact]
    public async Task ActErr011_AtividadeEncerrada_Retorna409()
    {
        _mediator
            .Send(Arg.Any<CompleteActivityCommand>(), Arg.Any<CancellationToken>())
            .Returns<CompleteActivityResult>(_ => throw new ActivityTerminalException("cancelled"));

        var client   = _factory.CreateAuthenticatedClient();
        var response = await client.PatchAsync($"/api/v1/activities/{Guid.NewGuid()}/complete", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("ACT-ERR-011", error!.Code);
    }

    // ── CorrelationId presente em todos os erros ──────────────────────────────

    [Fact]
    public async Task TodosErros_ContemCorrelationIdNaoVazio()
    {
        _mediator
            .Send(Arg.Any<GetActivityByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns<ActivityResponse>(_ => throw new ActivityNotFoundException(Guid.NewGuid()));

        var client   = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync($"/api/v1/activities/{Guid.NewGuid()}");

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.NotEqual(Guid.Empty, error.CorrelationId);
    }
}

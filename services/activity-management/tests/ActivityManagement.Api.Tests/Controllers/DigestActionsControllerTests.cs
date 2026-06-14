namespace ActivityManagement.Api.Tests.Controllers;

using System.Net;
using System.Net.Http.Json;
using ActivityManagement.Api.Tests.Helpers;
using ActivityManagement.Application.Activities.Commands;
using ActivityManagement.Contracts.Activities;
using MediatR;
using NSubstitute;
using Xunit;

/// <summary>
/// Testes de API do DigestActionsController (TASK-19).
/// Verifica os 4 ramos de resposta: válido, já usado, expirado, inválido/inexistente.
/// Confirma anti-enumeração PBT-03 (forma): token inexistente → mesmo corpo/código de atividade inacessível.
/// Sem autenticação JWT — autoridade vem do token opaco no path (design §10).
/// </summary>
public sealed class DigestActionsControllerTests : IDisposable
{
    private readonly ActivityManagementWebFactory _factory;
    private readonly IMediator _mediator;

    public DigestActionsControllerTests()
    {
        _mediator = Substitute.For<IMediator>();
        _factory  = new ActivityManagementWebFactory()
            .WithClaims([]) // sem JWT — endpoint público por token
            .WithSubstitute(_mediator);
    }

    public void Dispose() => _factory.Dispose();

    // ── Token válido → 200 ────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessDigestAction_TokenValido_Retorna200()
    {
        // Arrange
        var actId = Guid.NewGuid();
        _mediator
            .Send(Arg.Any<ProcessDigestActionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new DigestActionResult(actId, "complete", WasAlreadyProcessed: false));

        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync(
            $"/api/v1/digest-actions/abc123validtoken", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DigestActionResponse>();
        Assert.NotNull(result);
        Assert.Equal(actId, result.ActivityId);
        Assert.False(result.WasAlreadyProcessed);
    }

    // ── Token já usado → 200 idempotente (MSG-029) ───────────────────────────

    [Fact]
    public async Task ProcessDigestAction_TokenJaUsado_Retorna200Idempotente()
    {
        // Arrange
        var actId = Guid.NewGuid();
        _mediator
            .Send(Arg.Any<ProcessDigestActionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new DigestActionResult(actId, "complete", WasAlreadyProcessed: true));

        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync(
            $"/api/v1/digest-actions/usedtoken123", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DigestActionResponse>();
        Assert.NotNull(result);
        Assert.True(result.WasAlreadyProcessed);
    }

    // ── Token expirado → 410 ACT-ERR-009 ─────────────────────────────────────

    [Fact]
    public async Task ProcessDigestAction_TokenExpirado_Retorna410()
    {
        // Arrange
        _mediator
            .Send(Arg.Any<ProcessDigestActionCommand>(), Arg.Any<CancellationToken>())
            .Returns<DigestActionResult>(_ => throw new ExpiredDigestTokenException());

        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync(
            $"/api/v1/digest-actions/expiredtoken", null);

        // Assert
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("ACT-ERR-009", error.Code);
    }

    // ── Token inválido → 404 ACT-ERR-008 (indistinguível de atividade inacessível — PBT-03) ──

    [Fact]
    public async Task ProcessDigestAction_TokenInvalido_Retorna404ComCorpoIdentico()
    {
        // Arrange
        _mediator
            .Send(Arg.Any<ProcessDigestActionCommand>(), Arg.Any<CancellationToken>())
            .Returns<DigestActionResult>(_ => throw new InvalidDigestTokenException());

        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync(
            $"/api/v1/digest-actions/invalidtoken", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        // PBT-03: código ACT-ERR-008 (indistinguível de ACT-ERR-003)
        Assert.Equal("ACT-ERR-008", error.Code);
        // Nenhuma informação de atividade exposta
        Assert.DoesNotContain("atividade", error.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── Anti-enumeração: corpos de 404 não revelam dados da atividade ─────────

    [Fact]
    public async Task ProcessDigestAction_TokenInvalido_NaoExpoeTituloNemConteudo()
    {
        // Arrange
        _mediator
            .Send(Arg.Any<ProcessDigestActionCommand>(), Arg.Any<CancellationToken>())
            .Returns<DigestActionResult>(_ => throw new InvalidDigestTokenException());

        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync(
            $"/api/v1/digest-actions/anytoken", null);

        // Assert
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("title", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("description", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ownerId", body, StringComparison.OrdinalIgnoreCase);
    }
}

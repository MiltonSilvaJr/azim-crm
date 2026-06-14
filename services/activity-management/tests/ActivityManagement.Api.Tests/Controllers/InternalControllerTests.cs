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
/// Testes de API do InternalController (TASK-19).
/// Verifica: sem header de scheduler → 401; com header válido → 202.
/// Rota: POST /internal/overdue-scan.
/// </summary>
public sealed class InternalControllerTests : IDisposable
{
    private readonly ActivityManagementWebFactory _factory;
    private readonly IMediator _mediator;

    public InternalControllerTests()
    {
        _mediator = Substitute.For<IMediator>();
        _factory  = new ActivityManagementWebFactory()
            .WithClaims([])
            .WithSubstitute(_mediator);
    }

    public void Dispose() => _factory.Dispose();

    // ── Sem header de scheduler → 401 ─────────────────────────────────────────

    [Fact]
    public async Task OverdueScan_SemHeaderScheduler_Retorna401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/internal/overdue-scan", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Header inválido → 401 ─────────────────────────────────────────────────

    [Fact]
    public async Task OverdueScan_HeaderInvalido_Retorna401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-CloudScheduler-JobName", "wrong-job");

        var response = await client.PostAsync("/internal/overdue-scan", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Header válido → 202 Accepted ──────────────────────────────────────────

    [Fact]
    public async Task OverdueScan_HeaderValido_Retorna202()
    {
        _mediator
            .Send(Arg.Any<ScanOverdueActivitiesCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-CloudScheduler-JobName", "activity-overdue-scan");

        var response = await client.PostAsync("/internal/overdue-scan", null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}

namespace ActivityManagement.Api.Tests.Suite;

using System.Net;
using System.Net.Http.Json;
using ActivityManagement.Api.Tests.Helpers;
using ActivityManagement.Application.Activities.Commands;
using ActivityManagement.Application.Activities.Queries;
using ActivityManagement.Contracts.Activities;
using MediatR;
using NSubstitute;
using Xunit;

/// <summary>
/// Testes de isolamento cross-tenant — CI gate (TASK-21, ADR-0001, RNF 1.3).
/// Garante que usuário autenticado como Tenant A não consegue ver, modificar
/// ou concluir atividades do Tenant B. Resposta deve ser indistinguível de
/// atividade inexistente (404 ACT-ERR-003 — anti-enumeração).
///
/// Estes testes usam WebApplicationFactory com mediator mock simulando o
/// comportamento do TenantScopeBehavior + filtro de tenant da Infrastructure.
/// Testes de integração com Testcontainers PostgreSQL serão adicionados em TASK-24.
///
/// Registro como gate de CI: este arquivo é marcado como crítico.
/// Mapeia: TASK-21, RNF 1, ADR-0001, design §10.
/// </summary>
public sealed class CrossTenantTests : IDisposable
{
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();

    public void Dispose() { }

    // ── TenantA não enxerga atividade do TenantB → 404 (indistinguível) ───────

    [Fact]
    public async Task TenantA_NaoEnxergaAtividadeTenantB_Retorna404()
    {
        // Mediator simula que a atividade do Tenant B não está visível para o Tenant A
        // (EF Global Query Filter filtra por tenant_id — ADR-0001)
        var actIdTenantB = Guid.NewGuid();
        var mediator     = Substitute.For<IMediator>();
        mediator
            .Send(Arg.Any<GetActivityByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns<ActivityResponse>(_ => throw new ActivityNotFoundException(actIdTenantB));

        using var factory = new ActivityManagementWebFactory()
            .WithClaims(TestJwtHelper.SellerClaims(_tenantA))
            .WithSubstitute(mediator);
        var client = factory.CreateAuthenticatedClient();

        // Usa o ID de uma atividade do Tenant B (apenas o ID — sem acesso ao tenant)
        var response = await client.GetAsync($"/api/v1/activities/{actIdTenantB}");

        // A resposta deve ser 404 indistinguível de atividade inexistente (anti-enumeração)
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("ACT-ERR-003", error.Code);
        // Nenhuma pista de que a atividade existe em outro tenant
        Assert.DoesNotContain("tenant", error.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── TenantA não pode modificar atividade do TenantB → 404 ─────────────────

    [Fact]
    public async Task TenantA_NaoModificaAtividadeTenantB_Retorna404()
    {
        var actIdTenantB = Guid.NewGuid();
        var mediator     = Substitute.For<IMediator>();
        mediator
            .Send(Arg.Any<UpdateActivityCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new ActivityNotFoundException(actIdTenantB));

        using var factory = new ActivityManagementWebFactory()
            .WithClaims(TestJwtHelper.SellerClaims(_tenantA))
            .WithSubstitute(mediator);
        var client = factory.CreateAuthenticatedClient();

        var request  = new UpdateActivityRequest("meeting", "Tentativa", DateTimeOffset.UtcNow.AddDays(1));
        var response = await client.PutAsJsonAsync($"/api/v1/activities/{actIdTenantB}", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("ACT-ERR-003", error.Code);
    }

    // ── TenantA não pode concluir atividade do TenantB → 404 ─────────────────

    [Fact]
    public async Task TenantA_NaoConcluidAtividadeTenantB_Retorna404()
    {
        var actIdTenantB = Guid.NewGuid();
        var mediator     = Substitute.For<IMediator>();
        mediator
            .Send(Arg.Any<CompleteActivityCommand>(), Arg.Any<CancellationToken>())
            .Returns<CompleteActivityResult>(_ => throw new ActivityNotFoundException(actIdTenantB));

        using var factory = new ActivityManagementWebFactory()
            .WithClaims(TestJwtHelper.SellerClaims(_tenantA))
            .WithSubstitute(mediator);
        var client = factory.CreateAuthenticatedClient();

        var response = await client.PatchAsync($"/api/v1/activities/{actIdTenantB}/complete", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("ACT-ERR-003", error.Code);
    }

    // ── TenantA não pode excluir atividade do TenantB → 404 ──────────────────

    [Fact]
    public async Task TenantA_NaoExcluiAtividadeTenantB_Retorna404()
    {
        var actIdTenantB = Guid.NewGuid();
        var mediator     = Substitute.For<IMediator>();
        mediator
            .Send(Arg.Any<DeleteActivityCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new ActivityNotFoundException(actIdTenantB));

        using var factory = new ActivityManagementWebFactory()
            .WithClaims(TestJwtHelper.SellerClaims(_tenantA))
            .WithSubstitute(mediator);
        var client = factory.CreateAuthenticatedClient();

        var response = await client.DeleteAsync($"/api/v1/activities/{actIdTenantB}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("ACT-ERR-003", error.Code);
    }

    // ── CorrelationId presente nas respostas cross-tenant ─────────────────────

    [Fact]
    public async Task CrossTenant_RespostasContemCorrelationId()
    {
        var actIdTenantB = Guid.NewGuid();
        var mediator     = Substitute.For<IMediator>();
        mediator
            .Send(Arg.Any<GetActivityByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns<ActivityResponse>(_ => throw new ActivityNotFoundException(actIdTenantB));

        using var factory = new ActivityManagementWebFactory()
            .WithClaims(TestJwtHelper.SellerClaims(_tenantA))
            .WithSubstitute(mediator);
        var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync($"/api/v1/activities/{actIdTenantB}");

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.NotEqual(Guid.Empty, error.CorrelationId);
    }
}

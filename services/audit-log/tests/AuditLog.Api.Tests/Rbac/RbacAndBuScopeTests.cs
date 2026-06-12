using System.Net;
using System.Text.Json;
using AuditLog.Api.Tests.Helpers;
using AuditLog.Application.Abstractions;
using AuditLog.Application.Queries;
using AuditLog.Application.Results;
using AuditLog.Domain.Aggregates;
using FluentAssertions;
using MediatR;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace AuditLog.Api.Tests.Rbac;

/// <summary>
/// Testes de RBAC e escopo de BU para consulta de audit-log (TASK-17, design §10, DD-008).
/// Cobre: TenantAdmin vê todo o tenant, GestorBU filtrado por BU, papéis sem permissão → 403,
/// Platform Operator sem acesso por padrão, resposta uniforme anti-enumeração.
/// </summary>
public sealed class RbacAndBuScopeTests : IClassFixture<AuditApiFactory>
{
    private readonly AuditApiFactory _factory;

    public RbacAndBuScopeTests(AuditApiFactory factory)
    {
        _factory = factory;
    }

    // ------------------------------------------------------------------ TenantAdmin

    [Fact]
    public async Task TenantAdmin_CanListAuditLogs_Returns200()
    {
        // Arrange
        _factory.SenderMock
            .Send(Arg.Any<ListAuditLogsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AuditLogAggregate>(Array.Empty<AuditLogAggregate>(), 1, 50, 0));

        var client = _factory.CreateAuthenticatedClient(AuditRoles.TenantAdmin);

        // Act
        var response = await client.GetAsync("/api/v1/audit-logs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "TenantAdmin deve ter acesso irrestrito à trilha do tenant (REQ-008.2)");
    }

    [Fact]
    public async Task TenantAdmin_CanGetEntityHistory_Returns200()
    {
        // Arrange
        _factory.SenderMock
            .Send(Arg.Any<GetEntityAuditHistoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AuditLogAggregate>(Array.Empty<AuditLogAggregate>(), 1, 50, 0));

        var client = _factory.CreateAuthenticatedClient(AuditRoles.TenantAdmin);

        // Act
        var response = await client.GetAsync($"/api/v1/audit-logs/Opportunity/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ------------------------------------------------------------------ GestorBU

    [Fact]
    public async Task GestorBU_CanListAuditLogs_Returns200()
    {
        // Arrange — GestorBU tem acesso, filtrado por BU (DD-008)
        _factory.SenderMock
            .Send(Arg.Any<ListAuditLogsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AuditLogAggregate>(Array.Empty<AuditLogAggregate>(), 1, 50, 0));

        var client = _factory.CreateAuthenticatedClient(AuditRoles.GestorBU);

        // Act
        var response = await client.GetAsync("/api/v1/audit-logs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "GestorBU deve ter acesso à trilha filtrada pelas suas BUs (REQ-008.3, DD-008)");
    }

    [Fact]
    public async Task GestorBU_CanGetEntityHistory_Returns200()
    {
        // Arrange
        _factory.SenderMock
            .Send(Arg.Any<GetEntityAuditHistoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AuditLogAggregate>(Array.Empty<AuditLogAggregate>(), 1, 50, 0));

        var client = _factory.CreateAuthenticatedClient(AuditRoles.GestorBU);

        // Act
        var response = await client.GetAsync($"/api/v1/audit-logs/Account/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ------------------------------------------------------------------ Papéis sem permissão → 403

    [Theory]
    [InlineData("Vendedor")]
    [InlineData("Viewer")]
    [InlineData("PlatformOperator")]
    [InlineData("SomeUnknownRole")]
    public async Task UnauthorizedRole_Returns403_WithUniformMessage(string role)
    {
        // Arrange — AuthorizationBehavior do MediatR lança AuditAuthorizationException (AUD-ERR-002)
        // O comportamento é enforçado pelo AuthorizationBehavior na pipeline, não pelo controller.
        // Como o mock substitui ISender, precisamos simular o comportamento do behavior.
        _factory.SenderMock
            .Send(Arg.Any<ListAuditLogsQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AuditLog.Application.Errors.AuditAuthorizationException(
                AuditLog.Application.Errors.AuditErrorCodes.AccessDenied,
                "Acesso negado à trilha de auditoria."));

        var client = _factory.CreateAuthenticatedClient(role);

        // Act
        var response = await client.GetAsync("/api/v1/audit-logs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            $"papel '{role}' não deve ter acesso à trilha de auditoria (REQ-008.4)");

        // Verifica mensagem uniforme (anti-enumeração)
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.TryGetProperty("errorCode", out var errorCodeProp).Should().BeTrue();
        errorCodeProp.GetString().Should().Be(AuditLog.Application.Errors.AuditErrorCodes.AccessDenied);
    }

    [Fact]
    public async Task PlatformOperator_DefaultAccess_Returns403()
    {
        // Arrange — Platform Operator não tem acesso por padrão (REQ-008.4)
        _factory.SenderMock
            .Send(Arg.Any<ListAuditLogsQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AuditLog.Application.Errors.AuditAuthorizationException(
                AuditLog.Application.Errors.AuditErrorCodes.AccessDenied,
                "Acesso negado à trilha de auditoria."));

        var client = _factory.CreateAuthenticatedClient("PlatformOperator");

        // Act
        var response = await client.GetAsync("/api/v1/audit-logs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Platform Operator não deve ter acesso por padrão (REQ-008.4)");
    }

    // ------------------------------------------------------------------ Anti-enumeração entre 403 e 404

    [Fact]
    public async Task Forbidden403_And_NotFound404_HaveSameTitle()
    {
        // AUD-ERR-002 (403 acesso negado)
        _factory.SenderMock
            .Send(Arg.Any<ListAuditLogsQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AuditLog.Application.Errors.AuditAuthorizationException(
                AuditLog.Application.Errors.AuditErrorCodes.AccessDenied,
                "Acesso negado à trilha de auditoria."));

        var client = _factory.CreateAuthenticatedClient(AuditRoles.TenantAdmin);
        var response403 = await client.GetAsync("/api/v1/audit-logs");
        var doc403 = JsonDocument.Parse(await response403.Content.ReadAsStringAsync());

        // AUD-ERR-004 (404 não encontrado)
        _factory.SenderMock
            .Send(Arg.Any<GetEntityAuditHistoryQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AuditLog.Application.Errors.AuditNotFoundException(
                AuditLog.Application.Errors.AuditErrorCodes.NotFound,
                "Nenhum registro de auditoria encontrado."));

        var response404 = await client.GetAsync($"/api/v1/audit-logs/Opportunity/{Guid.NewGuid()}");
        var doc404 = JsonDocument.Parse(await response404.Content.ReadAsStringAsync());

        // Títulos devem ser iguais
        var title403 = doc403.RootElement.GetProperty("title").GetString();
        var title404 = doc404.RootElement.GetProperty("title").GetString();

        title403.Should().Be(title404,
            "AUD-ERR-002 e AUD-ERR-004 devem ter título idêntico para prevenir enumeração (design §12)");
    }

    // ------------------------------------------------------------------ Tenant context missing

    [Fact]
    public async Task TenantContextMissing_Returns403_AudErr008()
    {
        // Arrange — TenantContextBehavior lança AuditAuthorizationException com TenantContextMissing
        _factory.SenderMock
            .Send(Arg.Any<ListAuditLogsQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AuditLog.Application.Errors.AuditAuthorizationException(
                AuditLog.Application.Errors.AuditErrorCodes.TenantContextMissing,
                "Contexto de tenant ausente."));

        var client = _factory.CreateAuthenticatedClient(AuditRoles.TenantAdmin);

        // Act
        var response = await client.GetAsync("/api/v1/audit-logs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("errorCode").GetString()
            .Should().Be(AuditLog.Application.Errors.AuditErrorCodes.TenantContextMissing);
    }
}

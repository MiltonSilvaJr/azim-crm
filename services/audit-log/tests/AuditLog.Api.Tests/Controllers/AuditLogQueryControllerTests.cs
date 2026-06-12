using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuditLog.Api.Tests.Helpers;
using AuditLog.Application.Abstractions;
using AuditLog.Application.Queries;
using AuditLog.Application.Results;
using AuditLog.Domain.Aggregates;
using FluentAssertions;
using MediatR;
using NSubstitute;
using Xunit;

namespace AuditLog.Api.Tests.Controllers;

/// <summary>
/// Testes de integração para <c>AuditLogQueryController</c> (TASK-15).
/// Cobre: 200 com envelope paginado, 401 sem JWT, 405 em verbos de escrita,
/// mapeamento de query params e resposta de histórico de entidade.
/// </summary>
public sealed class AuditLogQueryControllerTests : IClassFixture<AuditApiFactory>
{
    private readonly AuditApiFactory _factory;

    public AuditLogQueryControllerTests(AuditApiFactory factory)
    {
        _factory = factory;
    }

    // ------------------------------------------------------------------ GET /api/v1/audit-logs

    [Fact]
    public async Task GetAuditLogs_WithTenantAdmin_Returns200WithPagedEnvelope()
    {
        // Arrange
        var items = new[]
        {
            AuditLogAggregateBuilder.Build(),
            AuditLogAggregateBuilder.Build()
        };

        _factory.SenderMock
            .Send(Arg.Any<ListAuditLogsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AuditLogAggregate>(items, 1, 50, 2));

        var client = _factory.CreateAuthenticatedClient(AuditRoles.TenantAdmin);

        // Act
        var response = await client.GetAsync("/api/v1/audit-logs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        doc.RootElement.GetProperty("page").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("pageSize").GetInt32().Should().Be(50);
        doc.RootElement.GetProperty("total").GetInt32().Should().Be(2);
        doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task GetAuditLogs_ItemShape_HasRequiredFields()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);
        var item = AuditLogAggregateBuilder.Build(createdAt: createdAt);

        _factory.SenderMock
            .Send(Arg.Any<ListAuditLogsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AuditLogAggregate>(new[] { item }, 1, 50, 1));

        var client = _factory.CreateAuthenticatedClient(AuditRoles.TenantAdmin);

        // Act
        var response = await client.GetAsync("/api/v1/audit-logs");
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var firstItem = doc.RootElement.GetProperty("items")[0];

        // Assert — campos obrigatórios do AuditLogResponse
        firstItem.TryGetProperty("id", out _).Should().BeTrue("id é obrigatório");
        firstItem.TryGetProperty("userId", out _).Should().BeTrue("userId é obrigatório");
        firstItem.TryGetProperty("entityType", out _).Should().BeTrue("entityType é obrigatório");
        firstItem.TryGetProperty("entityId", out _).Should().BeTrue("entityId é obrigatório");
        firstItem.TryGetProperty("action", out var actionProp).Should().BeTrue("action é obrigatório");
        firstItem.TryGetProperty("delta", out _).Should().BeTrue("delta é obrigatório");
        firstItem.TryGetProperty("createdAt", out var createdAtProp).Should().BeTrue("createdAt é obrigatório");

        // action deve ser lowercase (camelCase JsonStringEnumConverter)
        actionProp.GetString().Should().BeOneOf("create", "update", "delete");

        // createdAt em UTC ISO-8601
        createdAtProp.GetString().Should().EndWith("Z",
            "createdAt deve ser UTC ISO-8601 terminando em Z");
    }

    [Fact]
    public async Task GetAuditLogs_WithGestorBU_Returns200()
    {
        // Arrange
        _factory.SenderMock
            .Send(Arg.Any<ListAuditLogsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AuditLogAggregate>(Array.Empty<AuditLogAggregate>(), 1, 50, 0));

        var client = _factory.CreateAuthenticatedClient(AuditRoles.GestorBU);

        // Act
        var response = await client.GetAsync("/api/v1/audit-logs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAuditLogs_WithoutJwt_Returns401()
    {
        // Arrange
        var client = _factory.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/audit-logs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task WriteVerbs_OnAuditLogs_Returns405(string method)
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient(AuditRoles.TenantAdmin);
        var request = new HttpRequestMessage(new HttpMethod(method), "/api/v1/audit-logs");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed,
            $"verbo {method} não deve ser permitido na trilha de auditoria (REQ-007.1)");
    }

    [Fact]
    public async Task GetAuditLogs_MapsQueryParams_SendsCorrectQuery()
    {
        // Arrange
        _factory.SenderMock
            .Send(Arg.Any<ListAuditLogsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AuditLogAggregate>(Array.Empty<AuditLogAggregate>(), 2, 10, 0));

        var client = _factory.CreateAuthenticatedClient(AuditRoles.TenantAdmin);

        // Act
        var response = await client.GetAsync(
            "/api/v1/audit-logs?entityType=Opportunity&page=2&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verifica que ISender recebeu a query com params corretos
        await _factory.SenderMock.Received(1)
            .Send(Arg.Is<ListAuditLogsQuery>(q =>
                q.EntityType == "Opportunity" &&
                q.Page == 2 &&
                q.PageSize == 10),
                Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------ GET /api/v1/audit-logs/{entityType}/{entityId}

    [Fact]
    public async Task GetEntityAuditHistory_WithTenantAdmin_Returns200WithEnvelope()
    {
        // Arrange
        var entityId = AuditApiFactory.DefaultEntityId;
        var item = AuditLogAggregateBuilder.Build(entityType: "Opportunity", entityId: entityId);

        _factory.SenderMock
            .Send(Arg.Any<GetEntityAuditHistoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AuditLogAggregate>(new[] { item }, 1, 50, 1));

        var client = _factory.CreateAuthenticatedClient(AuditRoles.TenantAdmin);

        // Act
        var response = await client.GetAsync($"/api/v1/audit-logs/Opportunity/{entityId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
        doc.RootElement.GetProperty("total").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task GetEntityAuditHistory_WithoutJwt_Returns401()
    {
        // Arrange
        var client = _factory.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync(
            $"/api/v1/audit-logs/Opportunity/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task WriteVerbs_OnEntityHistory_Returns405(string method)
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient(AuditRoles.TenantAdmin);
        var request = new HttpRequestMessage(
            new HttpMethod(method),
            $"/api/v1/audit-logs/Opportunity/{Guid.NewGuid()}");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task GetEntityAuditHistory_MapsPathParams_SendsCorrectQuery()
    {
        // Arrange
        var entityId = AuditApiFactory.DefaultEntityId;

        _factory.SenderMock
            .Send(Arg.Any<GetEntityAuditHistoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AuditLogAggregate>(Array.Empty<AuditLogAggregate>(), 1, 50, 0));

        var client = _factory.CreateAuthenticatedClient(AuditRoles.TenantAdmin);

        // Act
        await client.GetAsync($"/api/v1/audit-logs/Opportunity/{entityId}");

        // Assert
        await _factory.SenderMock.Received(1)
            .Send(Arg.Is<GetEntityAuditHistoryQuery>(q =>
                q.EntityType == "Opportunity" &&
                q.EntityId == entityId),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAuditLogs_PaginationDefaults_AreCorrect()
    {
        // Arrange
        _factory.SenderMock
            .Send(Arg.Any<ListAuditLogsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AuditLogAggregate>(Array.Empty<AuditLogAggregate>(), 1, 50, 0));

        var client = _factory.CreateAuthenticatedClient(AuditRoles.TenantAdmin);

        // Act
        await client.GetAsync("/api/v1/audit-logs");

        // Assert — default page=1, pageSize=50
        await _factory.SenderMock.Received(1)
            .Send(Arg.Is<ListAuditLogsQuery>(q => q.Page == 1 && q.PageSize == 50),
                Arg.Any<CancellationToken>());
    }
}

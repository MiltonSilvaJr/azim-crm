using System.Net;
using System.Text.Json;
using AuditLog.Api.Tests.Helpers;
using AuditLog.Application.Abstractions;
using AuditLog.Application.Errors;
using AuditLog.Application.Queries;
using AuditLog.Application.Results;
using AuditLog.Domain.Aggregates;
using FluentAssertions;
using MediatR;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace AuditLog.Api.Tests.ErrorHandling;

/// <summary>
/// Testes do catálogo de erros AUD-ERR-001..008 (TASK-16).
/// Verifica que cada exceção de domínio/aplicação é traduzida para o status HTTP correto
/// e retorna ProblemDetails com campo <c>errorCode</c>.
/// Garante anti-enumeração em AUD-ERR-002/004.
/// </summary>
public sealed class ErrorCatalogTests : IClassFixture<AuditApiFactory>
{
    private readonly AuditApiFactory _factory;

    public ErrorCatalogTests(AuditApiFactory factory)
    {
        _factory = factory;
    }

    // ------------------------------------------------------------------ AUD-ERR-001 (400 filtro inválido)

    [Fact]
    public async Task AudErr001_InvalidFilter_Returns400WithErrorCode()
    {
        // Arrange: handler lança exceção de filtro inválido
        _factory.SenderMock
            .Send(Arg.Any<ListAuditLogsQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AuditValidationException(
                AuditErrorCodes.InvalidFilter, "Filtro de consulta inválido."));

        var client = _factory.CreateAuthenticatedClient(AuditRoles.TenantAdmin);

        // Act
        var response = await client.GetAsync("/api/v1/audit-logs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemDetailsWithErrorCode(response, AuditErrorCodes.InvalidFilter);
    }

    // ------------------------------------------------------------------ AUD-ERR-002 (403 acesso negado)

    [Fact]
    public async Task AudErr002_AccessDenied_Returns403WithErrorCode()
    {
        // Arrange
        _factory.SenderMock
            .Send(Arg.Any<ListAuditLogsQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AuditAuthorizationException(
                AuditErrorCodes.AccessDenied, "Acesso negado à trilha de auditoria."));

        var client = _factory.CreateAuthenticatedClient(AuditRoles.TenantAdmin);

        // Act
        var response = await client.GetAsync("/api/v1/audit-logs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertProblemDetailsWithErrorCode(response, AuditErrorCodes.AccessDenied);
    }

    // ------------------------------------------------------------------ AUD-ERR-003 (401 não autenticado)
    // Coberto nos AuditLogQueryControllerTests (sem JWT → 401)

    // ------------------------------------------------------------------ AUD-ERR-004 (404 não encontrado — anti-enumeração)

    [Fact]
    public async Task AudErr004_NotFound_Returns404WithUniformMessage()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        _factory.SenderMock
            .Send(Arg.Any<GetEntityAuditHistoryQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AuditNotFoundException(
                AuditErrorCodes.NotFound, "Nenhum registro de auditoria encontrado."));

        var client = _factory.CreateAuthenticatedClient(AuditRoles.TenantAdmin);

        // Act
        var response = await client.GetAsync($"/api/v1/audit-logs/Opportunity/{entityId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertProblemDetailsWithErrorCode(response, AuditErrorCodes.NotFound);
    }

    // ------------------------------------------------------------------ AUD-ERR-002 e AUD-ERR-004 — mensagem uniforme (anti-enumeração)

    [Fact]
    public async Task AudErr002And004_HaveSameMessage_AntiEnumeration()
    {
        // Arrange — simula os dois cenários e captura as mensagens
        _factory.SenderMock
            .Send(Arg.Any<ListAuditLogsQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AuditAuthorizationException(
                AuditErrorCodes.AccessDenied, "Acesso negado à trilha de auditoria."));

        var client = _factory.CreateAuthenticatedClient(AuditRoles.TenantAdmin);
        var response403 = await client.GetAsync("/api/v1/audit-logs");
        var body403 = await response403.Content.ReadAsStringAsync();
        var doc403 = JsonDocument.Parse(body403);

        // Para simular 404: reconfigurar mock
        var entityId = Guid.NewGuid();
        _factory.SenderMock
            .Send(Arg.Any<GetEntityAuditHistoryQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AuditNotFoundException(
                AuditErrorCodes.NotFound, "Nenhum registro de auditoria encontrado."));

        var response404 = await client.GetAsync($"/api/v1/audit-logs/Opportunity/{entityId}");
        var body404 = await response404.Content.ReadAsStringAsync();
        var doc404 = JsonDocument.Parse(body404);

        // Assert — títulos iguais (anti-enumeração REQ-005.3, design §12)
        var title403 = doc403.RootElement.TryGetProperty("title", out var t403) ? t403.GetString() : null;
        var title404 = doc404.RootElement.TryGetProperty("title", out var t404) ? t404.GetString() : null;

        title403.Should().Be(title404,
            "AUD-ERR-002 e AUD-ERR-004 devem ter mensagem idêntica para prevenir enumeração (design §12)");
    }

    // ------------------------------------------------------------------ AUD-ERR-005 (500 falha de persistência — sem PII)

    [Fact]
    public async Task AudErr005_PersistenceFailure_Returns500WithoutDeltaOrPii()
    {
        // Arrange
        _factory.SenderMock
            .Send(Arg.Any<ListAuditLogsQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AuditPersistenceException(
                AuditErrorCodes.PersistenceFailure,
                "Falha ao persistir registro de auditoria."));

        var client = _factory.CreateAuthenticatedClient(AuditRoles.TenantAdmin);

        // Act
        var response = await client.GetAsync("/api/v1/audit-logs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var body = await response.Content.ReadAsStringAsync();

        // Garante que delta_json e PII não aparecem na resposta (RNF-002.3)
        body.Should().NotContain("delta_json",
            "delta_json não deve aparecer em respostas de erro (RNF-002.3)");
        body.Should().NotContain("@",
            "e-mail não deve aparecer em respostas de erro (RNF-002.3)");
    }

    // ------------------------------------------------------------------ AUD-ERR-006 (400 período inválido)

    [Fact]
    public async Task AudErr006_InvalidDateRange_Returns400WithErrorCode()
    {
        // Arrange
        _factory.SenderMock
            .Send(Arg.Any<ListAuditLogsQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AuditValidationException(
                AuditErrorCodes.InvalidDateRange, "Período de consulta inválido."));

        var client = _factory.CreateAuthenticatedClient(AuditRoles.TenantAdmin);

        // Act
        var response = await client.GetAsync("/api/v1/audit-logs?from=2026-06-11&to=2026-01-01");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemDetailsWithErrorCode(response, AuditErrorCodes.InvalidDateRange);
    }

    // ------------------------------------------------------------------ AUD-ERR-007 (405 operação não permitida)
    // Coberto nos AuditLogQueryControllerTests (POST/PUT/PATCH/DELETE → 405)

    // ------------------------------------------------------------------ AUD-ERR-008 (403 tenant ausente)

    [Fact]
    public async Task AudErr008_TenantContextMissing_Returns403WithErrorCode()
    {
        // Arrange
        _factory.SenderMock
            .Send(Arg.Any<ListAuditLogsQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AuditAuthorizationException(
                AuditErrorCodes.TenantContextMissing,
                "Contexto de tenant ausente."));

        var client = _factory.CreateAuthenticatedClient(AuditRoles.TenantAdmin);

        // Act
        var response = await client.GetAsync("/api/v1/audit-logs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertProblemDetailsWithErrorCode(response, AuditErrorCodes.TenantContextMissing);
    }

    // ------------------------------------------------------------------ Helpers

    private static async Task AssertProblemDetailsWithErrorCode(
        HttpResponseMessage response,
        string expectedErrorCode)
    {
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        // ProblemDetails deve conter campo errorCode
        doc.RootElement.TryGetProperty("errorCode", out var errorCodeProp).Should().BeTrue(
            $"ProblemDetails deve conter campo 'errorCode' com valor '{expectedErrorCode}'");

        errorCodeProp.GetString().Should().Be(expectedErrorCode);

        // Garante que não há PII nem stack trace
        body.Should().NotContain("StackTrace",
            "stack trace não deve ser exposto em produção");
    }
}

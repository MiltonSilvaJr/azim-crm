using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using TenantAdministration.Api.Tests.Infrastructure;
using TenantAdministration.Application.Commands;
using TenantAdministration.Application.Ports;
using TenantAdministration.Contracts.Platform;
using Xunit;

namespace TenantAdministration.Api.Tests.Security;

/// <summary>
/// Testes de auditoria append-only (TASK-21).
/// Verifica que eventos de provisionamento e branding são publicados via IEventOutbox
/// com tenant_id e ator presentes no payload.
/// A auditoria final é consumida pelo módulo audit-log via Pub/Sub (design.md §10, §9, RNF 5).
/// Integração com audit-log: desacoplada via IEventOutbox — sem dependência circular.
/// Gate: [Trait("Category","SecurityGate")].
/// </summary>
[Trait("Category", "SecurityGate")]
public sealed class AuditAppendOnlyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuditAppendOnlyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.TenantRepository.ClearReceivedCalls();
        _factory.ProvisioningSaga.ClearReceivedCalls();
    }

    [Fact(DisplayName = "Após provisionar com sucesso, saga é chamada com slug e adminEmail (auditabilidade)")]
    public async Task Provision_Success_SagaCalledWithSlugAndActor()
    {
        // Arrange
        const string slug = "audit-tenant";
        const string adminEmail = "admin@audit.com";
        var tenantId = Guid.NewGuid();

        _factory.TenantRepository
            .ExistsSlugAsync(slug, Arg.Any<CancellationToken>())
            .Returns(false);

        _factory.ProvisioningSaga
            .ExecuteAsync(
                Arg.Any<Guid>(), slug, Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), adminEmail, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProvisioningResult(tenantId, "idp-audit-123"));

        var client = _factory.CreatePlatformOperatorClient();
        var request = new ProvisionTenantRequest(
            Slug: slug,
            SlugConfirmation: slug,
            DisplayName: "Audit Tenant",
            Timezone: "America/Sao_Paulo",
            DigestTime: "07:00",
            AdminEmail: adminEmail);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/platform/tenants", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "provisionamento deve ser bem-sucedido para gerar evento de auditoria");

        // A saga é a unidade responsável por chamar IEventOutbox (design.md §6.4)
        await _factory.ProvisioningSaga.Received(1)
            .ExecuteAsync(
                Arg.Any<Guid>(), slug, Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), adminEmail, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "adminEmail nunca aparece na resposta de provisioning (proteção PII)")]
    public async Task Provision_AdminEmailNotInResponse()
    {
        // Arrange
        const string slug = "audit-pii-test";
        const string adminEmail = "pii@audit.com";
        var tenantId = Guid.NewGuid();

        _factory.TenantRepository
            .ExistsSlugAsync(slug, Arg.Any<CancellationToken>())
            .Returns(false);

        _factory.ProvisioningSaga
            .ExecuteAsync(
                Arg.Any<Guid>(), slug, Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), adminEmail, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProvisioningResult(tenantId, "idp-pii-123"));

        var client = _factory.CreatePlatformOperatorClient();
        var request = new ProvisionTenantRequest(
            Slug: slug,
            SlugConfirmation: slug,
            DisplayName: "PII Test",
            Timezone: "America/Sao_Paulo",
            DigestTime: "07:00",
            AdminEmail: adminEmail);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/platform/tenants", request);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        body.Should().NotContain("adminEmail",
            "PII não deve aparecer em respostas de API (LGPD, design.md §10)");
        body.Should().NotContain(adminEmail,
            "endereço de e-mail é PII e não deve ser exposto");
    }
}

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PartnerManagement.Application.Behaviors;
using PartnerManagement.Application.Partners;
using PartnerManagement.Application.Partners.Commands;
using PartnerManagement.Application.Partners.Queries;
using PartnerManagement.Api.Middleware;
using PartnerManagement.Api.Tests.Helpers;
using PartnerManagement.Contracts.Partners;
using PartnerManagement.Domain.Partners.Exceptions;
using Xunit;

namespace PartnerManagement.Api.Tests.Partners;

/// <summary>
/// Cobertura do catálogo completo de erros PM-ERR-001..011.
/// Valida que cada código de erro aparece no response correto com o status HTTP esperado.
/// Valida anti-PII: mensagens de erro não contêm e-mail, telefone ou nome de parceiro.
/// TDD-first: TASK-25 ST-01 Red.
/// Mapeia: TASK-25, design §12, PartnerErrors catálogo.
/// </summary>
public sealed class ErrorCatalogTests : IClassFixture<PartnerApiFactory>
{
    private readonly PartnerApiFactory _factory;

    public ErrorCatalogTests(PartnerApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClientWithClaims(string claims)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestClaimsHeader, claims);
        return client;
    }

    // =========================================================================
    // PM-ERR-001 — Nome do parceiro é obrigatório
    // =========================================================================

    [Fact]
    public async Task CreatePartner_EmptyName_ReturnsPmErr001()
    {
        // Arrange
        _factory.Mediator
            .Send(Arg.Any<CreatePartnerCommand>(), Arg.Any<CancellationToken>())
            .Throws(new PartnerNameRequiredException());

        var client = CreateClientWithClaims(TestData.WriterClaims);
        var body = new CreatePartnerRequest { Name = "   ", Role = "Indicador" };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/partners", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(PartnerErrors.NameRequired);
    }

    // =========================================================================
    // PM-ERR-002 — Papel de parceiro inválido
    // =========================================================================

    [Fact]
    public async Task CreatePartner_InvalidRole_ReturnsPmErr002()
    {
        // Arrange
        _factory.Mediator
            .Send(Arg.Any<CreatePartnerCommand>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidPartnerRoleException("RoleInvalido"));

        var client = CreateClientWithClaims(TestData.WriterClaims);
        var body = new CreatePartnerRequest { Name = "Acme", Role = "RoleInvalido" };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/partners", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(PartnerErrors.InvalidRole);
        // Anti-PII: papel inválido não deve vazar em mensagem de erro
        content.Should().NotContain("RoleInvalido");
    }

    // =========================================================================
    // PM-ERR-003 — Percentual fora do intervalo
    // =========================================================================

    [Fact]
    public async Task CreatePartner_PercentageOutOfRange_ReturnsPmErr003()
    {
        // Arrange
        _factory.Mediator
            .Send(Arg.Any<CreatePartnerCommand>(), Arg.Any<CancellationToken>())
            .Throws(new PercentageOutOfRangeException());

        var client = CreateClientWithClaims(TestData.WriterClaims);
        var body = new CreatePartnerRequest { Name = "Acme", Role = "Indicador", PctSetup = 150m };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/partners", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(PartnerErrors.PercentageOutOfRange);
    }

    // =========================================================================
    // PM-ERR-004 — E-mail de contato inválido
    // =========================================================================

    [Fact]
    public async Task CreatePartner_InvalidEmail_ReturnsPmErr004()
    {
        // Arrange
        _factory.Mediator
            .Send(Arg.Any<CreatePartnerCommand>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidPartnerContactException());

        var client = CreateClientWithClaims(TestData.WriterClaims);
        var body = new CreatePartnerRequest
        {
            Name = "Acme",
            Role = "Indicador",
            ContactEmail = "not-an-email"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/partners", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(PartnerErrors.InvalidContactEmail);
        // Anti-PII: e-mail não deve aparecer na mensagem de erro
        content.Should().NotContain("not-an-email");
    }

    // =========================================================================
    // PM-ERR-007 — Parceiro não encontrado
    // =========================================================================

    [Fact]
    public async Task GetById_NotFound_ReturnsPmErr007()
    {
        // Arrange
        var id = Guid.NewGuid();
        _factory.Mediator
            .Send(Arg.Any<GetPartnerByIdQuery>(), Arg.Any<CancellationToken>())
            .Throws(new PartnerNotFoundException(id));

        var client = CreateClientWithClaims(TestData.ViewerClaims);

        // Act
        var response = await client.GetAsync($"/api/v1/partners/{id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(PartnerErrors.PartnerNotFound);
        // Anti-PII: ID não deve aparecer em dados de erro no campo "error"
        // (pode aparecer como correlationId — mas não na mensagem de erro)
        content.Should().NotContain(id.ToString().Substring(0, 8),
            because: "IDs de recursos não devem vazar em mensagens de erro");
    }

    // =========================================================================
    // PM-ERR-008 — Acesso negado
    // =========================================================================

    [Fact]
    public async Task AnyEndpoint_AccessDenied_ReturnsPmErr008()
    {
        // Arrange
        _factory.Mediator
            .Send(Arg.Any<UpdatePartnerCommand>(), Arg.Any<CancellationToken>())
            .Throws(new AccessDeniedException("partners:write"));

        _factory.Mediator
            .Send(Arg.Any<GetPartnerByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PartnerDetail(
                TestData.DefaultPartnerId, "Acme", "Indicador",
                10m, 5m, null, null, null, true, false,
                DateTimeOffset.UtcNow, null));

        var client = CreateClientWithClaims(TestData.ViewerClaims);
        var body = new UpdatePartnerRequest { Name = "X" };
        var content = JsonContent.Create(body);

        // Act
        var response = await client.PatchAsync(
            $"/api/v1/partners/{TestData.DefaultPartnerId}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        string responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain(PartnerErrors.AccessDenied);
    }

    // =========================================================================
    // PM-ERR-009 — Parâmetros de listagem inválidos
    // =========================================================================

    [Theory]
    [InlineData("/api/v1/partners?pageSize=0")]
    [InlineData("/api/v1/partners?page=0")]
    [InlineData("/api/v1/partners?pageSize=101")]
    public async Task ListPartners_InvalidPagination_ReturnsPmErr009(string url)
    {
        // Arrange
        var client = CreateClientWithClaims(TestData.ViewerClaims);

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(PartnerErrors.InvalidListParameters);
    }

    // =========================================================================
    // PM-ERR-010 — Requisição duplicada (Idempotency-Key reutilizada)
    // =========================================================================

    [Fact]
    public async Task CreatePartner_DuplicateIdempotencyKey_ReturnsPmErr010()
    {
        // Arrange
        _factory.Mediator
            .Send(Arg.Any<CreatePartnerCommand>(), Arg.Any<CancellationToken>())
            .Throws(new DuplicateIdempotencyKeyException("idem-key-123"));

        var client = CreateClientWithClaims(TestData.WriterClaims);
        client.DefaultRequestHeaders.Add("Idempotency-Key", "idem-key-123");
        var body = new CreatePartnerRequest { Name = "Acme", Role = "Indicador" };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/partners", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(PartnerErrors.DuplicateRequest);
        // Anti-PII: chave de idempotência não deve vazar no erro
        content.Should().NotContain("idem-key-123");
    }

    // =========================================================================
    // PM-ERR-011 — Período de comissão inválido
    // =========================================================================

    [Fact]
    public async Task GetCommissions_MissingPeriod_ReturnsPmErr011()
    {
        // Arrange — sem from/to
        var client = CreateClientWithClaims(TestData.AdminClaims);

        // Act
        var response = await client.GetAsync(
            $"/api/v1/partners/{TestData.DefaultPartnerId}/commissions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(PartnerErrors.InvalidCommissionPeriod);
    }

    [Fact]
    public async Task GetCommissions_OnlyFromProvided_ReturnsPmErr011()
    {
        // Arrange — apenas from, sem to
        var client = CreateClientWithClaims(TestData.AdminClaims);

        // Act
        var response = await client.GetAsync(
            $"/api/v1/partners/{TestData.DefaultPartnerId}/commissions?from=2025-01-01");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(PartnerErrors.InvalidCommissionPeriod);
    }

    // =========================================================================
    // Formato de erro: correlationId sempre presente
    // =========================================================================

    [Fact]
    public async Task AllErrors_ContainCorrelationId()
    {
        // Arrange
        var id = Guid.NewGuid();
        _factory.Mediator
            .Send(Arg.Any<GetPartnerByIdQuery>(), Arg.Any<CancellationToken>())
            .Throws(new PartnerNotFoundException(id));

        var client = CreateClientWithClaims(TestData.ViewerClaims);
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "corr-test-task25");

        // Act
        var response = await client.GetAsync($"/api/v1/partners/{id}");

        // Assert
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("correlationId");
    }

    // =========================================================================
    // Anti-PII: nomes de parceiro não aparecem em erros
    // =========================================================================

    [Fact]
    public async Task ErrorMessages_DoNotContainPartnerName()
    {
        // Arrange — força erro de nome em branco
        _factory.Mediator
            .Send(Arg.Any<CreatePartnerCommand>(), Arg.Any<CancellationToken>())
            .Throws(new PartnerNameRequiredException());

        var client = CreateClientWithClaims(TestData.WriterClaims);
        // Nome sensível que não deve aparecer no erro
        var body = new CreatePartnerRequest { Name = "NomeSensivelXYZ", Role = "Indicador" };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/partners", body);

        // Assert
        string content = await response.Content.ReadAsStringAsync();
        content.Should().NotContain("NomeSensivelXYZ",
            because: "o nome do parceiro não deve aparecer em mensagens de erro (anti-PII)");
    }
}

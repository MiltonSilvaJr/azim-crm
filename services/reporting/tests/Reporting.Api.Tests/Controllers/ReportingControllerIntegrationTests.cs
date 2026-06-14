using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Reporting.Application.Ports;
using Reporting.Contracts.ReadModels;
using Reporting.Contracts.Responses;
using Reporting.Domain.Enums;
using Reporting.Domain.ValueObjects;
using Xunit;

namespace Reporting.Api.Tests.Controllers;

/// <summary>
/// Testes de integração do <c>ReportingController</c> (TASK-21 e TASK-22).
///
/// Cenários cobertos:
/// <list type="bullet">
///   <item><description>401 sem token de autenticação.</description></item>
///   <item><description>403 REPORT-ERR-005 para Platform Operator.</description></item>
///   <item><description>400 REPORT-ERR-001 para período inválido (from > to).</description></item>
///   <item><description>404 REPORT-ERR-004 anti-enumeração (buId fora do escopo).</description></item>
///   <item><description>400 REPORT-ERR-003 para tipo de relatório inválido no export.</description></item>
///   <item><description>200 com payload correto para Tenant Admin.</description></item>
///   <item><description>X-Correlation-Id propagado no response.</description></item>
/// </list>
///
/// Mapeia: TASK-21, TASK-22, design §8.2, §12, RNF 4.3, Req 7, ADR-0001.
/// </summary>
public sealed class ReportingControllerIntegrationTests
{
    private static readonly Guid _tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _userId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid _allowedBuId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    // ─── Autenticação ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFunnel_WithoutAuth_Returns401()
    {
        using var factory = CreateFactory(null); // sem auth
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/v1/reports/funnel");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetForecast_WithoutAuth_Returns401()
    {
        using var factory = CreateFactory(null);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/v1/reports/forecast");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetRanking_WithoutAuth_Returns401()
    {
        using var factory = CreateFactory(null);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/v1/reports/ranking");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetChannels_WithoutAuth_Returns401()
    {
        using var factory = CreateFactory(null);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/v1/reports/channels");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCommissions_WithoutAuth_Returns401()
    {
        using var factory = CreateFactory(null);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/v1/reports/commissions");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetExport_WithoutAuth_Returns401()
    {
        using var factory = CreateFactory(null);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/v1/reports/funnel/export");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Platform Operator bloqueado ──────────────────────────────────────────

    [Theory]
    [InlineData("/api/v1/reports/funnel")]
    [InlineData("/api/v1/reports/forecast")]
    [InlineData("/api/v1/reports/ranking")]
    [InlineData("/api/v1/reports/channels")]
    [InlineData("/api/v1/reports/commissions")]
    [InlineData("/api/v1/reports/funnel/export")]
    public async Task AllEndpoints_PlatformOperator_Returns403WithReportErr005(string path)
    {
        using var factory = CreateFactory(ReportingRole.PlatformOperator);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("REPORT-ERR-005");
        // Mensagem não deve conter PII nem dados de outro tenant (RNF 4.3)
        body.Should().NotContain("@");
    }

    // ─── Período inválido ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetFunnel_FromAfterTo_Returns400WithReportErr001()
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/v1/reports/funnel?from=2026-06-30&to=2026-01-01");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("REPORT-ERR-001");
    }

    // ─── Anti-enumeração ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetFunnel_BuIdOutOfScope_Returns404WithReportErr004()
    {
        using var factory = CreateFactory(ReportingRole.GestorBU);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var outsideBuId = Guid.NewGuid(); // BU não pertencente ao escopo do gestor
        var response = await client.GetAsync($"/api/v1/reports/funnel?from=2026-01-01&to=2026-06-30&buId={outsideBuId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("REPORT-ERR-004",
            "anti-enumeração: buId fora do escopo retorna 404 genérico sem revelar existência");
    }

    // ─── Export — tipo inválido ────────────────────────────────────────────────

    [Fact]
    public async Task GetExport_InvalidReportType_Returns400WithReportErr003()
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/v1/reports/invalido/export?from=2026-01-01&to=2026-06-30");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("REPORT-ERR-003");
    }

    // ─── 200 Tenant Admin ────────────────────────────────────────────────────

    [Fact]
    public async Task GetFunnel_TenantAdmin_Returns200WithValidPayload()
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/v1/reports/funnel?from=2026-01-01&to=2026-06-30");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.TryGetProperty("stages", out _).Should().BeTrue(
            "response do funil deve conter campo 'stages'");
    }

    [Fact]
    public async Task GetForecast_TenantAdmin_Returns200()
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/v1/reports/forecast?from=2026-01-01&to=2026-06-30");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRanking_TenantAdmin_Returns200()
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/v1/reports/ranking?from=2026-01-01&to=2026-06-30");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetChannels_TenantAdmin_Returns200()
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/v1/reports/channels?from=2026-01-01&to=2026-06-30");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCommissions_TenantAdmin_Returns200()
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/v1/reports/commissions?from=2026-01-01&to=2026-06-30");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetExport_TenantAdmin_Returns200WithSignedUrl()
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/v1/reports/funnel/export?from=2026-01-01&to=2026-06-30");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.TryGetProperty("signedUrl", out var urlProp).Should().BeTrue();
        urlProp.GetString().Should().NotBeNullOrEmpty();
    }

    // ─── X-Correlation-Id ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetFunnel_Response_ContainsCorrelationId()
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/v1/reports/funnel?from=2026-01-01&to=2026-06-30");

        response.Headers.TryGetValues("X-Correlation-Id", out var values).Should().BeTrue(
            "X-Correlation-Id deve estar presente em todo response (design §8.1, RNF 6)");
        values!.First().Should().NotBeNullOrEmpty();
    }

    // ─── Ausência de PII nos erros ────────────────────────────────────────────

    [Fact]
    public async Task ErrorResponse_DoesNotContainPii()
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        // Período inválido gera erro
        var response = await client.GetAsync("/api/v1/reports/funnel?from=2026-12-31&to=2026-01-01");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotMatchRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
            "mensagem de erro não deve conter endereço de e-mail (PII)");
    }

    // ─── Default de período ────────────────────────────────────────────────────

    [Fact]
    public async Task GetFunnel_WithoutPeriodFilter_UsesCurrentMonth()
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        // Sem parâmetros from/to — deve usar mês corrente como default
        var response = await client.GetAsync("/api/v1/reports/funnel");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ─── Factory helper ───────────────────────────────────────────────────────

    /// <summary>
    /// Cria a <see cref="WebApplicationFactory{TEntryPoint}"/> configurada para o papel especificado.
    /// Quando <paramref name="role"/> é <c>null</c>, nenhuma autenticação é configurada (→ 401).
    /// </summary>
    private WebApplicationFactory<Reporting.Api.AssemblyReference> CreateFactory(
        ReportingRole? role)
    {
        return new WebApplicationFactory<Reporting.Api.AssemblyReference>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");

                builder.ConfigureServices(services =>
                {
                    ConfigureTestRepositories(services);

                    if (role.HasValue)
                    {
                        ConfigureTestAuth(services, role.Value);
                    }
                    else
                    {
                        // Sem autenticação: usa esquema que sempre falha
                        services.AddAuthentication("NoAuth")
                            .AddScheme<AuthenticationSchemeOptions, NoOpAuthHandler>("NoAuth", _ => { });
                    }
                });
            });
    }

    /// <summary>Registra repositórios e storage stub para isolamento de testes.</summary>
    private void ConfigureTestRepositories(IServiceCollection services)
    {
        // IReportingReadRepository: retorna coleções vazias
        var repoMock = Substitute.For<IReportingReadRepository>();
        repoMock.GetFunnelAsync(Arg.Any<ReportScope>(), Arg.Any<Period>(), Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FunnelRow>>([]));
        repoMock.GetForecastAsync(Arg.Any<ReportScope>(), Arg.Any<Period>(), Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ForecastRow>>([]));
        repoMock.GetRankingAsync(Arg.Any<ReportScope>(), Arg.Any<Period>(), Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RankingRow>>([]));
        repoMock.GetChannelAsync(Arg.Any<ReportScope>(), Arg.Any<Period>(), Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ChannelRow>>([]));
        repoMock.GetCommissionsAsync(Arg.Any<ReportScope>(), Arg.Any<Period>(), Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CommissionRow>>([]));

        services.AddSingleton(repoMock);

        // ICsvStorage: stub in-memory com URL assinada fictícia
        var csvStorageMock = Substitute.For<ICsvStorage>();
        csvStorageMock.UploadAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CsvUploadResult(
                "https://storage.googleapis.com/azim-reports/test.csv?X-Goog-Expires=900",
                DateTimeOffset.UtcNow.AddMinutes(15),
                "test.csv")));

        services.AddSingleton(csvStorageMock);
    }

    /// <summary>Configura autenticação de teste com o papel especificado.</summary>
    private void ConfigureTestAuth(IServiceCollection services, ReportingRole role)
    {
        // IScopeResolver: retorna scope resolvido para o papel
        var scopeResolver = Substitute.For<IScopeResolver>();

        if (role == ReportingRole.PlatformOperator)
        {
            // PlatformOperator: scope resolver lança UnauthorizedAccessException (RNF 5)
            scopeResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<ReportScope>(
                    new UnauthorizedAccessException("PlatformOperator negado. (RNF 5)")));
        }
        else
        {
            var scope = ReportScope.Create(
                _tenantId,
                role,
                role == ReportingRole.GestorBU ? [_allowedBuId] : [],
                role == ReportingRole.Vendedor ? _userId : null);

            scopeResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(scope));
        }

        services.AddSingleton(scopeResolver);

        // Handler JWT de teste com claims de tenant e papel
        services.AddAuthentication("TestScheme")
            .AddScheme<TestAuthHandlerOptions, TestAuthHandler>("TestScheme", opts =>
            {
                opts.TenantId = _tenantId;
                opts.UserId = _userId;
                opts.Role = role.ToString();
            });
    }
}

// ─── Handlers de autenticação de teste ───────────────────────────────────────

/// <summary>
/// Handler de autenticação que sempre retorna 401 (sem autenticação).
/// Usado para testar cenários de ausência de token.
/// </summary>
internal sealed class NoOpAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public NoOpAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.Fail("Sem autenticação no cliente de teste sem auth."));
}

/// <summary>
/// Opções do handler de autenticação de teste com papel e identidade configuráveis.
/// </summary>
internal sealed class TestAuthHandlerOptions : AuthenticationSchemeOptions
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "TenantAdmin";
}

/// <summary>
/// Handler de autenticação de teste que cria um <see cref="ClaimsPrincipal"/> com as claims
/// de tenant_id, sub e role — simula o JWT real em ambiente de teste.
/// Mapeia: design §8.1, ADR-0001.
/// </summary>
internal sealed class TestAuthHandler : AuthenticationHandler<TestAuthHandlerOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<TestAuthHandlerOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim("tenant_id", Options.TenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, Options.UserId.ToString()),
            new Claim("sub", Options.UserId.ToString()),
            new Claim(ClaimTypes.Role, Options.Role),
            new Claim("role", Options.Role)
        };

        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

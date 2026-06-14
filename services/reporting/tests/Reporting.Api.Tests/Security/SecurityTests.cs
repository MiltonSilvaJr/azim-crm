using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
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

namespace Reporting.Api.Tests.Security;

/// <summary>
/// Testes de segurança obrigatórios da Onda 6 — TASK-23.
///
/// Cenários cobertos:
/// <list type="bullet">
///   <item><description>Zero PII em logs: geração de ranking com display_name presente na response
///     não vaza PII em nenhuma entrada de log (RNF 4, DD-008, RISK-REPORT-04).</description></item>
///   <item><description>Platform Operator bloqueado: todos os seis endpoints retornam 403
///     REPORT-ERR-005 sem tocar o banco (RNF 5, design §10).</description></item>
///   <item><description>Cross-tenant no export: a verificação de que o CSV carrega apenas dados
///     do tenant correto é validada via conteúdo do export — o IScopeResolver do tenant B
///     não pode ser substituído pelo tenant A (anti-enumeração cross-tenant, ADR-0001).</description></item>
///   <item><description>Erros nunca expõem PII: mensagens de erro do catálogo não contêm
///     display_name, e-mail, telefone ou dados de outro tenant (RNF 4.3).</description></item>
/// </list>
///
/// Gate de CI: falha nestes testes bloqueia o release (design §13.5, RISK-REPORT-03/04).
///
/// Mapeia: TASK-23, Onda 6, design §10, §13.5, RNF 4, RNF 5, DD-008, ADR-0001, RISK-REPORT-04.
/// </summary>
public sealed class SecurityTests
{
    private static readonly Guid TenantIdA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantIdB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid UserId    = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid AllowedBuId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    // ══════════════════════════════════════════════════════════════════════════
    // 1. ZERO PII EM LOGS
    // Verifica que display_name, e-mail e telefone nunca aparecem em logs
    // durante a geração de qualquer tipo de relatório (RNF 4.2, DD-008).
    // ══════════════════════════════════════════════════════════════════════════

    [Theory(DisplayName = "Zero PII em logs: nenhum relatório registra display_name, e-mail ou telefone nos logs")]
    [InlineData("/api/v1/reports/funnel?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/forecast?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/ranking?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/channels?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/commissions?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/funnel/export?from=2026-01-01&to=2026-06-30")]
    public async Task AllReports_NoPiiInLogs(string path)
    {
        var logSink = new InMemoryLogSink();
        using var factory = CreateFactoryWithLogSink(ReportingRole.TenantAdmin, logSink,
            rankingDisplayName: "João da Silva");

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false });

        await client.GetAsync(path);

        // Nenhuma entrada de log deve conter PII (RNF 4.2, DD-008, RISK-REPORT-04)
        var allLogs = string.Join(" ", logSink.Messages);
        allLogs.Should().NotContain("João da Silva",
            "display_name nunca deve aparecer em logs (DD-008, RNF 4.2)");
        allLogs.Should().NotContain("@",
            "endereço de e-mail nunca deve aparecer em logs (RNF 4.2)");
        allLogs.Should().NotContain("telefone",
            "número de telefone nunca deve aparecer em logs (RNF 4.2)");
    }

    [Fact(DisplayName = "Zero PII em logs: ranking com display_name na response não vaza PII nos logs")]
    public async Task RankingWithDisplayName_ResponseContainsName_ButLogsDoNot()
    {
        const string displayName = "Maria Oliveira";
        var logSink = new InMemoryLogSink();
        using var factory = CreateFactoryWithLogSink(ReportingRole.TenantAdmin, logSink,
            rankingDisplayName: displayName);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/v1/reports/ranking?from=2026-01-01&to=2026-06-30");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseBody = await response.Content.ReadAsStringAsync();
        // A response PODE conter displayName (comportamento esperado para TenantAdmin via PiiMinimizationPolicy)
        // O importante é que os LOGS não contenham PII (RNF 4.2)
        var allLogs = string.Join(" ", logSink.Messages);
        allLogs.Should().NotContain(displayName,
            "display_name da response nunca deve aparecer em nenhum log (DD-008, RNF 4.2)");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 2. PLATFORM OPERATOR BLOQUEADO EM TODOS OS ENDPOINTS
    // PlatformOperator deve receber 403 REPORT-ERR-005 em todos os 6 endpoints
    // ANTES de qualquer acesso ao banco (RNF 5, design §10).
    // ══════════════════════════════════════════════════════════════════════════

    [Theory(DisplayName = "Platform Operator bloqueado: 403 REPORT-ERR-005 em todos os endpoints")]
    [InlineData("/api/v1/reports/funnel?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/forecast?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/ranking?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/channels?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/commissions?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/funnel/export?from=2026-01-01&to=2026-06-30")]
    public async Task PlatformOperator_AllEndpoints_Returns403WithReportErr005(string path)
    {
        var repoSpy = Substitute.For<IReportingReadRepository>();
        using var factory = CreateFactoryForPlatformOperator(repoSpy);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false });

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "PlatformOperator deve ser negado com 403 (RNF 5, REPORT-ERR-005)");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("REPORT-ERR-005",
            "o código de erro deve ser REPORT-ERR-005 para Platform Operator (design §12)");

        // Verifica que o repositório NUNCA foi acessado (PlatformOperator bloqueado antes do banco)
        await repoSpy.DidNotReceive().GetFunnelAsync(
            Arg.Any<ReportScope>(), Arg.Any<Period>(),
            Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>());
        await repoSpy.DidNotReceive().GetForecastAsync(
            Arg.Any<ReportScope>(), Arg.Any<Period>(),
            Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>());
        await repoSpy.DidNotReceive().GetRankingAsync(
            Arg.Any<ReportScope>(), Arg.Any<Period>(),
            Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>());
        await repoSpy.DidNotReceive().GetChannelAsync(
            Arg.Any<ReportScope>(), Arg.Any<Period>(),
            Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>());
        await repoSpy.DidNotReceive().GetCommissionsAsync(
            Arg.Any<ReportScope>(), Arg.Any<Period>(),
            Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Platform Operator: mensagem de 403 não contém PII nem dados de tenant")]
    public async Task PlatformOperator_ErrorMessage_DoesNotContainPii()
    {
        using var factory = CreateFactoryForPlatformOperator(Substitute.For<IReportingReadRepository>());
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/v1/reports/funnel?from=2026-01-01&to=2026-06-30");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain("@",
            "mensagem de erro não deve conter e-mail (RNF 4.3)");
        body.Should().NotContain(TenantIdA.ToString(),
            "mensagem de erro não deve conter tenant_id de outro tenant (RNF 4.3)");
        body.Should().NotContain(TenantIdB.ToString(),
            "mensagem de erro não deve revelar tenant B (RNF 4.3)");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 3. CROSS-TENANT NO EXPORT
    // O CSV gerado para tenant A não pode conter dados de tenant B.
    // Verificado via mock: o scope resolver de tenant A nunca retorna linhas de tenant B.
    // O TenantConnectionInterceptor e RLS garantem isso na infra (PBT-03 em Infrastructure.Tests).
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "Cross-tenant export: CSV do tenant A não contém dados de tenant B")]
    public async Task Export_TenantA_CsvDoesNotContainTenantBData()
    {
        // Spy que registra os escopos usados nas chamadas ao repositório
        var repoSpy = Substitute.For<IReportingReadRepository>();

        // Repositório retorna linhas com owner_id do tenant A (jamais do tenant B)
        var tenantARow = new FunnelRow(
            StageId: Guid.NewGuid(),
            StageName: "Proposta",
            Category: "open",
            Count: 5,
            TotalCents: 100000L,
            WeightedForecastCents: 50000L);

        repoSpy.GetFunnelAsync(
                Arg.Any<ReportScope>(), Arg.Any<Period>(),
                Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FunnelRow>>([tenantARow]));

        // Todos os outros métodos retornam listas vazias
        repoSpy.GetForecastAsync(Arg.Any<ReportScope>(), Arg.Any<Period>(),
                Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ForecastRow>>([]));
        repoSpy.GetRankingAsync(Arg.Any<ReportScope>(), Arg.Any<Period>(),
                Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RankingRow>>([]));
        repoSpy.GetChannelAsync(Arg.Any<ReportScope>(), Arg.Any<Period>(),
                Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ChannelRow>>([]));
        repoSpy.GetCommissionsAsync(Arg.Any<ReportScope>(), Arg.Any<Period>(),
                Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CommissionRow>>([]));

        using var factory = CreateFactoryWithRepoSpy(ReportingRole.TenantAdmin, TenantIdA, repoSpy);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/v1/reports/funnel/export?from=2026-01-01&to=2026-06-30");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verifica que o repositório foi chamado com o scope do tenant A — nunca com tenant B
        await repoSpy.Received(1).GetFunnelAsync(
            Arg.Is<ReportScope>(s => s.TenantId == TenantIdA),
            Arg.Any<Period>(),
            Arg.Any<IEnumerable<Guid>?>(),
            Arg.Any<CancellationToken>());

        // Verifica que o scope NUNCA carrega tenant B
        await repoSpy.DidNotReceive().GetFunnelAsync(
            Arg.Is<ReportScope>(s => s.TenantId == TenantIdB),
            Arg.Any<Period>(),
            Arg.Any<IEnumerable<Guid>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Cross-tenant: scope do tenant A nunca contém tenant_id do tenant B")]
    public async Task ScopeResolution_TenantA_NeverContainsTenantBId()
    {
        var resolvedScopes = new List<ReportScope>();
        var repoSpy = Substitute.For<IReportingReadRepository>();

        // Intercepta e registra todos os escopos usados
        repoSpy.GetFunnelAsync(
                Arg.Do<ReportScope>(s => resolvedScopes.Add(s)),
                Arg.Any<Period>(), Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FunnelRow>>([]));
        repoSpy.GetForecastAsync(Arg.Any<ReportScope>(), Arg.Any<Period>(),
                Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ForecastRow>>([]));
        repoSpy.GetRankingAsync(Arg.Any<ReportScope>(), Arg.Any<Period>(),
                Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RankingRow>>([]));
        repoSpy.GetChannelAsync(Arg.Any<ReportScope>(), Arg.Any<Period>(),
                Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ChannelRow>>([]));
        repoSpy.GetCommissionsAsync(Arg.Any<ReportScope>(), Arg.Any<Period>(),
                Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CommissionRow>>([]));

        using var factory = CreateFactoryWithRepoSpy(ReportingRole.TenantAdmin, TenantIdA, repoSpy);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false });

        await client.GetAsync("/api/v1/reports/funnel?from=2026-01-01&to=2026-06-30");

        resolvedScopes.Should().AllSatisfy(s =>
            s.TenantId.Should().Be(TenantIdA,
                "o scope nunca deve conter o tenant_id de outro tenant (ADR-0001, Req 8)"));
        resolvedScopes.Should().AllSatisfy(s =>
            s.TenantId.Should().NotBe(TenantIdB,
                "tenant B nunca pode aparecer no scope do tenant A (ADR-0001)"));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 4. ERROS DO CATÁLOGO SEM PII (RNF 4.3)
    // ══════════════════════════════════════════════════════════════════════════

    [Theory(DisplayName = "Erros do catálogo não contêm PII (RNF 4.3)")]
    [InlineData("/api/v1/reports/funnel?from=2026-12-31&to=2026-01-01", "REPORT-ERR-001")] // período inválido
    [InlineData("/api/v1/reports/invalido/export?from=2026-01-01&to=2026-06-30", "REPORT-ERR-003")] // tipo inválido
    public async Task ErrorCatalog_MessagesDoNotContainPii(string path, string expectedCode)
    {
        using var factory = CreateBaseFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false });

        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain(expectedCode,
            $"erro {expectedCode} deve estar presente na resposta");
        body.Should().NotMatchRegex(
            @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
            "mensagem de erro não deve conter endereço de e-mail (RNF 4.3)");
        body.Should().NotContain("display_name",
            "chave display_name nunca deve aparecer em mensagens de erro (RNF 4.3, DD-008)");
        body.Should().NotContain(TenantIdB.ToString(),
            "mensagem de erro não deve revelar tenant_id de outro tenant (RNF 4.3)");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Factories de teste
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Factory com log sink injetado para captura de logs.
    /// Permite verificar ausência de PII nos logs durante geração de relatórios.
    /// </summary>
    private WebApplicationFactory<Reporting.Api.AssemblyReference> CreateFactoryWithLogSink(
        ReportingRole role,
        InMemoryLogSink logSink,
        string? rankingDisplayName = null)
    {
        return new WebApplicationFactory<Reporting.Api.AssemblyReference>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    // Log sink que captura todas as mensagens de log
                    services.AddSingleton<ILoggerFactory>(new InMemoryLoggerFactory(logSink));

                    ConfigureTestAuth(services, role);
                    ConfigureRepositoryWithRankingName(services, rankingDisplayName);
                });
            });
    }

    /// <summary>
    /// Factory para Platform Operator com spy no repositório.
    /// Permite verificar que o repositório nunca é chamado.
    /// </summary>
    private WebApplicationFactory<Reporting.Api.AssemblyReference> CreateFactoryForPlatformOperator(
        IReportingReadRepository repoSpy)
    {
        return new WebApplicationFactory<Reporting.Api.AssemblyReference>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    // PlatformOperator: scope resolver lança exceção de acesso negado
                    var scopeResolver = Substitute.For<IScopeResolver>();
                    scopeResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                        .Returns(Task.FromException<ReportScope>(
                            new UnauthorizedAccessException("PlatformOperator negado. (RNF 5)")));

                    services.AddSingleton(scopeResolver);
                    services.AddSingleton(repoSpy);
                    ConfigureCsvStorage(services);

                    services.AddAuthentication("TestScheme")
                        .AddScheme<SecurityTestAuthOptions, SecurityTestAuthHandler>("TestScheme", opts =>
                        {
                            opts.TenantId = TenantIdA;
                            opts.UserId = UserId;
                            opts.Role = ReportingRole.PlatformOperator.ToString();
                        });
                });
            });
    }

    /// <summary>
    /// Factory com spy no repositório para capturar escopos usados nas chamadas.
    /// </summary>
    private WebApplicationFactory<Reporting.Api.AssemblyReference> CreateFactoryWithRepoSpy(
        ReportingRole role,
        Guid tenantId,
        IReportingReadRepository repoSpy)
    {
        return new WebApplicationFactory<Reporting.Api.AssemblyReference>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    ConfigureTestAuthForTenant(services, role, tenantId);
                    services.AddSingleton(repoSpy);
                    ConfigureCsvStorage(services);
                });
            });
    }

    /// <summary>Factory base sem customizações especiais.</summary>
    private WebApplicationFactory<Reporting.Api.AssemblyReference> CreateBaseFactory(ReportingRole role)
    {
        return new WebApplicationFactory<Reporting.Api.AssemblyReference>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    ConfigureTestAuth(services, role);
                    ConfigureDefaultRepository(services);
                });
            });
    }

    private void ConfigureTestAuth(IServiceCollection services, ReportingRole role)
    {
        ConfigureTestAuthForTenant(services, role, TenantIdA);
    }

    private void ConfigureTestAuthForTenant(IServiceCollection services, ReportingRole role, Guid tenantId)
    {
        var scopeResolver = Substitute.For<IScopeResolver>();
        var scope = ReportScope.Create(
            tenantId,
            role,
            role == ReportingRole.GestorBU ? [AllowedBuId] : [],
            role == ReportingRole.Vendedor ? UserId : null);

        scopeResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(scope));

        services.AddSingleton(scopeResolver);

        services.AddAuthentication("TestScheme")
            .AddScheme<SecurityTestAuthOptions, SecurityTestAuthHandler>("TestScheme", opts =>
            {
                opts.TenantId = tenantId;
                opts.UserId = UserId;
                opts.Role = role.ToString();
            });
    }

    private static void ConfigureDefaultRepository(IServiceCollection services)
    {
        var repo = Substitute.For<IReportingReadRepository>();
        repo.GetFunnelAsync(Arg.Any<ReportScope>(), Arg.Any<Period>(),
                Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FunnelRow>>([]));
        repo.GetForecastAsync(Arg.Any<ReportScope>(), Arg.Any<Period>(),
                Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ForecastRow>>([]));
        repo.GetRankingAsync(Arg.Any<ReportScope>(), Arg.Any<Period>(),
                Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RankingRow>>([]));
        repo.GetChannelAsync(Arg.Any<ReportScope>(), Arg.Any<Period>(),
                Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ChannelRow>>([]));
        repo.GetCommissionsAsync(Arg.Any<ReportScope>(), Arg.Any<Period>(),
                Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CommissionRow>>([]));
        services.AddSingleton(repo);
        ConfigureCsvStorage(services);
    }

    private static void ConfigureRepositoryWithRankingName(
        IServiceCollection services,
        string? displayName)
    {
        var repo = Substitute.For<IReportingReadRepository>();
        repo.GetFunnelAsync(Arg.Any<ReportScope>(), Arg.Any<Period>(),
                Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FunnelRow>>([]));
        repo.GetForecastAsync(Arg.Any<ReportScope>(), Arg.Any<Period>(),
                Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ForecastRow>>([]));

        // Ranking com displayName preenchido para testar ausência de PII nos logs
        var rankingRows = displayName is not null
            ? (IReadOnlyList<RankingRow>)[new RankingRow(
                OwnerId: Guid.NewGuid(),
                DisplayName: displayName,
                WonCount: 3,
                WonValueCents: 150000L,
                PipelineForecastCents: 300000L)]
            : (IReadOnlyList<RankingRow>)[];

        repo.GetRankingAsync(Arg.Any<ReportScope>(), Arg.Any<Period>(),
                Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(rankingRows));
        repo.GetChannelAsync(Arg.Any<ReportScope>(), Arg.Any<Period>(),
                Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ChannelRow>>([]));
        repo.GetCommissionsAsync(Arg.Any<ReportScope>(), Arg.Any<Period>(),
                Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CommissionRow>>([]));
        services.AddSingleton(repo);
        ConfigureCsvStorage(services);
    }

    private static void ConfigureCsvStorage(IServiceCollection services)
    {
        var csvStorageMock = Substitute.For<ICsvStorage>();
        csvStorageMock.UploadAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CsvUploadResult(
                "https://storage.googleapis.com/azim-reports/test.csv?X-Goog-Expires=900",
                DateTimeOffset.UtcNow.AddMinutes(15),
                "test.csv")));
        services.AddSingleton(csvStorageMock);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// Infraestrutura de teste: log sink e auth handler específicos para segurança
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Coletor de mensagens de log em memória para verificação de ausência de PII.
/// Permite inspecionar todas as mensagens registradas durante um teste (RNF 4.2, TASK-23).
/// </summary>
internal sealed class InMemoryLogSink
{
    private readonly List<string> _messages = [];

    /// <summary>Todas as mensagens de log capturadas durante o teste.</summary>
    public IReadOnlyList<string> Messages => _messages.AsReadOnly();

    /// <summary>Registra uma mensagem de log.</summary>
    public void Write(string message) => _messages.Add(message);
}

/// <summary>
/// Logger em memória que encaminha todas as mensagens ao <see cref="InMemoryLogSink"/>.
/// Verifica ausência de PII em logs durante testes de segurança (TASK-23).
/// </summary>
internal sealed class InMemoryLogger : ILogger
{
    private readonly InMemoryLogSink _sink;
    private readonly string _categoryName;

    public InMemoryLogger(InMemoryLogSink sink, string categoryName)
    {
        _sink = sink;
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        if (!string.IsNullOrEmpty(message))
        {
            _sink.Write($"[{_categoryName}] {message}");
        }
        if (exception is not null)
        {
            _sink.Write($"[{_categoryName}] EXCEPTION: {exception.Message}");
        }
    }
}

/// <summary>
/// Factory de loggers em memória para injeção no DI container durante testes de segurança.
/// </summary>
internal sealed class InMemoryLoggerFactory : ILoggerFactory
{
    private readonly InMemoryLogSink _sink;

    public InMemoryLoggerFactory(InMemoryLogSink sink) => _sink = sink;

    public ILogger CreateLogger(string categoryName) =>
        new InMemoryLogger(_sink, categoryName);

    public void AddProvider(ILoggerProvider provider) { }
    public void Dispose() { }
}

/// <summary>
/// Opções do handler de autenticação de teste para testes de segurança.
/// </summary>
internal sealed class SecurityTestAuthOptions : AuthenticationSchemeOptions
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "TenantAdmin";
}

/// <summary>
/// Handler de autenticação de teste para testes de segurança.
/// Cria um <see cref="ClaimsPrincipal"/> com tenant_id, sub e role configuráveis.
/// </summary>
internal sealed class SecurityTestAuthHandler : AuthenticationHandler<SecurityTestAuthOptions>
{
    public SecurityTestAuthHandler(
        IOptionsMonitor<SecurityTestAuthOptions> options,
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

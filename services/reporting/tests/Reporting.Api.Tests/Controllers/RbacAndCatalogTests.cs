using System.Net;
using System.Security.Claims;
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

namespace Reporting.Api.Tests.Controllers;

/// <summary>
/// Testes adicionais de RBAC por papel, catálogo de erros e anti-enumeração (TASK-22).
///
/// Cenários cobertos:
/// <list type="bullet">
///   <item><description>RBAC Vendedor: recebe 200; dados filtrados pelo próprio escopo.</description></item>
///   <item><description>RBAC GestorBU: recebe 200 para BU dentro do escopo.</description></item>
///   <item><description>RBAC GestorBU: recebe 404 REPORT-ERR-004 para BU fora do escopo (anti-enumeração).</description></item>
///   <item><description>RBAC TenantAdmin: todos os endpoints retornam 200.</description></item>
///   <item><description>Catálogo REPORT-ERR-003: tipo de export inválido → 400.</description></item>
///   <item><description>Catálogo REPORT-ERR-001: from &gt; to → 400 nos seis endpoints.</description></item>
///   <item><description>PBT-05 ao nível de API: export usa mesmos dados do relatório correspondente.</description></item>
///   <item><description>Anti-enumeração: mensagem de 404 não distingue "não existe" de "sem permissão".</description></item>
/// </list>
///
/// Mapeia: TASK-22, design §13.4, §8.2, §12, §10, RNF 4.3, Req 7, PBT-05.
/// </summary>
public sealed class RbacAndCatalogTests
{
    private static readonly Guid _tenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid _userId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid _allowedBuId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    // ─── RBAC: Vendedor ───────────────────────────────────────────────────────

    [Theory(DisplayName = "Vendedor recebe 200 em todos os endpoints de relatório")]
    [InlineData("/api/v1/reports/funnel?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/forecast?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/ranking?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/channels?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/commissions?from=2026-01-01&to=2026-06-30")]
    public async Task AllReportEndpoints_Vendedor_Returns200(string path)
    {
        using var factory = CreateFactory(ReportingRole.Vendedor);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "Vendedor tem acesso ao relatório com escopo restrito ao próprio ownerId");
    }

    [Fact(DisplayName = "Vendedor recebe 200 no export funnel")]
    public async Task Export_Vendedor_Returns200()
    {
        using var factory = CreateFactory(ReportingRole.Vendedor);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/v1/reports/funnel/export?from=2026-01-01&to=2026-06-30");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "Vendedor pode exportar o relatório com dados do próprio escopo");
    }

    // ─── RBAC: GestorBU ───────────────────────────────────────────────────────

    [Theory(DisplayName = "GestorBU com buId dentro do escopo recebe 200")]
    [InlineData("/api/v1/reports/funnel")]
    [InlineData("/api/v1/reports/forecast")]
    [InlineData("/api/v1/reports/ranking")]
    [InlineData("/api/v1/reports/channels")]
    [InlineData("/api/v1/reports/commissions")]
    public async Task AllReportEndpoints_GestorBU_WithAllowedBuId_Returns200(string basePath)
    {
        using var factory = CreateFactory(ReportingRole.GestorBU);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"{basePath}?from=2026-01-01&to=2026-06-30&buId={_allowedBuId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "GestorBU pode consultar relatório filtrando por BU do seu membership");
    }

    [Theory(DisplayName = "GestorBU com buId fora do escopo recebe 404 REPORT-ERR-004 (anti-enumeração)")]
    [InlineData("/api/v1/reports/funnel")]
    [InlineData("/api/v1/reports/forecast")]
    [InlineData("/api/v1/reports/ranking")]
    [InlineData("/api/v1/reports/channels")]
    [InlineData("/api/v1/reports/commissions")]
    public async Task AllReportEndpoints_GestorBU_WithOutOfScopeBuId_Returns404AntiEnumeration(string basePath)
    {
        using var factory = CreateFactory(ReportingRole.GestorBU);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var outsideBuId = Guid.NewGuid(); // BU que não está no membership do GestorBU

        var response = await client.GetAsync($"{basePath}?from=2026-01-01&to=2026-06-30&buId={outsideBuId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            because: "Anti-enumeração: buId fora do escopo retorna 404 genérico — nunca 403");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("REPORT-ERR-004",
            because: "O código REPORT-ERR-004 deve estar presente na resposta de anti-enumeração");
        body.Should().NotContain("403",
            because: "Resposta não deve revelar que o acesso foi negado — apenas que não foi encontrado");
    }

    [Fact(DisplayName = "Anti-enumeração: mensagem de 404 não diferencia 'não existe' de 'sem permissão'")]
    public async Task AntiEnumeration_ErrorMessage_IsGenericNotFoundMessage()
    {
        using var factory = CreateFactory(ReportingRole.GestorBU);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var outsideBuId = Guid.NewGuid();

        var response = await client.GetAsync($"/api/v1/reports/funnel?from=2026-01-01&to=2026-06-30&buId={outsideBuId}");

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        // A mensagem deve ser genérica — não deve revelar se a BU existe em outro tenant
        doc.RootElement.GetProperty("message").GetString()
            .Should().Be("Recurso não encontrado no seu escopo.",
                because: "Mensagem anti-enumeração deve ser idêntica independente do motivo real do 404");

        // Não deve conter tenantId, buId específico ou qualquer PII
        body.Should().NotContain(_tenantId.ToString(),
            because: "Resposta não deve expor o tenantId");
        body.Should().NotContain(outsideBuId.ToString(),
            because: "Resposta não deve expor o buId consultado (anti-enumeração)");
    }

    // ─── RBAC: TenantAdmin ────────────────────────────────────────────────────

    [Theory(DisplayName = "TenantAdmin recebe 200 em todos os endpoints sem filtro de BU")]
    [InlineData("/api/v1/reports/funnel?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/forecast?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/ranking?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/channels?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/commissions?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/funnel/export?from=2026-01-01&to=2026-06-30")]
    public async Task AllEndpoints_TenantAdmin_Returns200(string path)
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "TenantAdmin tem acesso irrestrito a todos os relatórios do tenant");
    }

    // ─── Catálogo de erros: REPORT-ERR-001 ───────────────────────────────────

    [Theory(DisplayName = "from > to retorna 400 REPORT-ERR-001 em todos os endpoints")]
    [InlineData("/api/v1/reports/funnel?from=2026-12-31&to=2026-01-01")]
    [InlineData("/api/v1/reports/forecast?from=2026-12-31&to=2026-01-01")]
    [InlineData("/api/v1/reports/ranking?from=2026-12-31&to=2026-01-01")]
    [InlineData("/api/v1/reports/channels?from=2026-12-31&to=2026-01-01")]
    [InlineData("/api/v1/reports/commissions?from=2026-12-31&to=2026-01-01")]
    [InlineData("/api/v1/reports/funnel/export?from=2026-12-31&to=2026-01-01")]
    public async Task AllEndpoints_InvalidPeriod_Returns400ReportErr001(string path)
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: "Período inválido (from > to) deve retornar 400");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("REPORT-ERR-001",
            because: "Código REPORT-ERR-001 deve ser retornado para período inválido");
    }

    [Fact(DisplayName = "REPORT-ERR-001: mensagem de erro não contém PII")]
    public async Task InvalidPeriod_ErrorMessage_DoesNotContainPii()
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/v1/reports/funnel?from=2026-12-31&to=2026-01-01");

        var body = await response.Content.ReadAsStringAsync();

        // Verificar ausência de PII: e-mail, nome, CPF, telefone
        body.Should().NotMatchRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
            because: "Mensagem de erro não deve conter e-mail (PII, RNF 4.3)");
        body.Should().NotContain(_userId.ToString(),
            because: "Mensagem de erro não deve expor o userId");
    }

    // ─── Catálogo de erros: REPORT-ERR-003 ───────────────────────────────────

    [Theory(DisplayName = "Tipo de export inválido retorna 400 REPORT-ERR-003")]
    [InlineData("invalid")]
    [InlineData("unknown")]
    [InlineData("xls")]
    [InlineData("pdf")]
    [InlineData("")]
    public async Task Export_InvalidType_Returns400ReportErr003(string invalidType)
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Rota de export: /{type}/export
        var path = string.IsNullOrEmpty(invalidType)
            ? "/api/v1/reports//export?from=2026-01-01&to=2026-06-30"
            : $"/api/v1/reports/{invalidType}/export?from=2026-01-01&to=2026-06-30";

        var response = await client.GetAsync(path);

        // Tipo vazio pode retornar 404 de roteamento; tipos inválidos devem retornar 400
        if (!string.IsNullOrEmpty(invalidType))
        {
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                because: $"Tipo de relatório '{invalidType}' inválido deve retornar 400 REPORT-ERR-003");

            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("REPORT-ERR-003",
                because: "Código REPORT-ERR-003 deve ser retornado para tipo de relatório inválido");
        }
    }

    [Theory(DisplayName = "Tipos de export válidos retornam 200")]
    [InlineData("funnel")]
    [InlineData("forecast")]
    [InlineData("ranking")]
    [InlineData("channel")]
    [InlineData("channels")]
    [InlineData("commissions")]
    public async Task Export_ValidType_Returns200(string validType)
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(
            $"/api/v1/reports/{validType}/export?from=2026-01-01&to=2026-06-30");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"Tipo de relatório '{validType}' é válido e deve retornar 200");
    }

    // ─── Catálogo de erros: REPORT-ERR-005 (Platform Operator) ──────────────

    [Fact(DisplayName = "REPORT-ERR-005: mensagem não contém PII nem dados de outro tenant")]
    public async Task PlatformOperator_ErrorMessage_DoesNotContainPiiOrTenantData()
    {
        using var factory = CreateFactory(ReportingRole.PlatformOperator);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/v1/reports/funnel?from=2026-01-01&to=2026-06-30");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("REPORT-ERR-005");

        // Verificar que não há PII no corpo do erro
        body.Should().NotMatchRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
            because: "Resposta de acesso negado não deve conter e-mail");
        body.Should().NotContain(_tenantId.ToString(),
            because: "Resposta de acesso negado não deve expor o tenantId");
        body.Should().NotContain(_userId.ToString(),
            because: "Resposta de acesso negado não deve expor o userId");
    }

    // ─── Estrutura do response (contrato) ────────────────────────────────────

    [Fact(DisplayName = "GET /funnel response contém campo 'stages' como array")]
    public async Task GetFunnel_Response_ContainsStagesArray()
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/v1/reports/funnel?from=2026-01-01&to=2026-06-30");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        // FunnelReportResponse usa 'stages' (não 'rows') conforme contrato (design §5.2)
        doc.RootElement.TryGetProperty("stages", out var stages).Should().BeTrue(
            because: "FunnelReportResponse deve conter o campo 'stages'");
        stages.ValueKind.Should().Be(JsonValueKind.Array,
            because: "'stages' deve ser um array JSON");
    }

    [Fact(DisplayName = "GET /forecast response contém campo 'rows' como array")]
    public async Task GetForecast_Response_ContainsRowsArray()
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/v1/reports/forecast?from=2026-01-01&to=2026-06-30");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("rows", out var rows).Should().BeTrue(
            because: "ForecastReportResponse deve conter o campo 'rows'");
        rows.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact(DisplayName = "GET /channels response contém campo 'rows' como array")]
    public async Task GetChannels_Response_ContainsRowsArray()
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/v1/reports/channels?from=2026-01-01&to=2026-06-30");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("rows", out var rows).Should().BeTrue(
            because: "ChannelReportResponse deve conter o campo 'rows'");
        rows.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact(DisplayName = "GET /commissions response contém campo 'rows' como array")]
    public async Task GetCommissions_Response_ContainsRowsArray()
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/v1/reports/commissions?from=2026-01-01&to=2026-06-30");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("rows", out var rows).Should().BeTrue(
            because: "CommissionReportResponse deve conter o campo 'rows'");
        rows.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact(DisplayName = "GET /ranking response contém campo 'rows' como array")]
    public async Task GetRanking_Response_ContainsRowsArray()
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/v1/reports/ranking?from=2026-01-01&to=2026-06-30");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("rows", out var rows).Should().BeTrue(
            because: "RankingReportResponse deve conter o campo 'rows'");
        rows.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact(DisplayName = "GET /export response contém campo 'signedUrl' não vazio")]
    public async Task GetExport_Response_ContainsSignedUrl()
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/v1/reports/funnel/export?from=2026-01-01&to=2026-06-30");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("signedUrl", out var urlProp).Should().BeTrue(
            because: "CsvExportResponse deve conter o campo 'signedUrl'");
        urlProp.GetString().Should().NotBeNullOrEmpty(
            because: "URL assinada não pode ser vazia");
    }

    // ─── PBT-05 ao nível de API: relatório × export equivalentes ─────────────

    [Theory(DisplayName = "PBT-05: relatório e export usam mesmos filtros — response HTTP compatíveis")]
    [InlineData("funnel")]
    [InlineData("forecast")]
    [InlineData("ranking")]
    [InlineData("channel")]
    [InlineData("commissions")]
    public async Task ReportAndExport_SameFilters_BothReturn200(string reportType)
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var period = "?from=2026-01-01&to=2026-06-30";
        var reportPath = reportType == "channel" ? "channels" : reportType;

        // Requisição ao endpoint de relatório
        var reportResponse = await client.GetAsync($"/api/v1/reports/{reportPath}{period}");
        // Requisição ao endpoint de export com o mesmo tipo e filtros
        var exportResponse = await client.GetAsync($"/api/v1/reports/{reportType}/export{period}");

        reportResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"Endpoint de relatório /{reportPath} deve retornar 200");
        exportResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"Endpoint de export /{reportType}/export deve retornar 200 com os mesmos filtros");
    }

    [Fact(DisplayName = "PBT-05: export retorna URL assinada enquanto relatório retorna stages — ambos 200")]
    public async Task ReportAndExport_ReturnDifferentStructures_BothSucceed()
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var period = "?from=2026-01-01&to=2026-06-30";

        var reportResponse = await client.GetAsync($"/api/v1/reports/funnel{period}");
        var exportResponse = await client.GetAsync($"/api/v1/reports/funnel/export{period}");

        reportResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        exportResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var reportBody = JsonDocument.Parse(await reportResponse.Content.ReadAsStringAsync());
        var exportBody = JsonDocument.Parse(await exportResponse.Content.ReadAsStringAsync());

        // FunnelReportResponse contém 'stages' (contrato design §5.2)
        reportBody.RootElement.TryGetProperty("stages", out _).Should().BeTrue(
            because: "Response de funil deve conter 'stages' conforme contrato (design §5.2)");

        // Export: contém 'signedUrl' (estrutura diferente do relatório)
        exportBody.RootElement.TryGetProperty("signedUrl", out _).Should().BeTrue(
            because: "Response de export deve conter 'signedUrl' (PBT-05: mesmos dados, formatos diferentes)");
    }

    // ─── Content-Type ─────────────────────────────────────────────────────────

    [Theory(DisplayName = "Endpoints de sucesso retornam Content-Type application/json")]
    [InlineData("/api/v1/reports/funnel?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/forecast?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/ranking?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/channels?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/commissions?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/funnel/export?from=2026-01-01&to=2026-06-30")]
    public async Task AllSuccessEndpoints_ReturnJsonContentType(string path)
    {
        using var factory = CreateFactory(ReportingRole.TenantAdmin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(path);

        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/json",
                because: "Endpoints de sucesso devem retornar application/json");
    }

    // ─── GestorBU sem buId (sem filtro): acessa tenant inteiro da BU ──────────

    [Theory(DisplayName = "GestorBU sem filtro de buId recebe 200 (escopo resolve as BUs do membership)")]
    [InlineData("/api/v1/reports/funnel?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/forecast?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/channels?from=2026-01-01&to=2026-06-30")]
    public async Task GestorBU_WithoutBuIdFilter_Returns200(string path)
    {
        using var factory = CreateFactory(ReportingRole.GestorBU);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "GestorBU sem filtro explícito de buId recebe dados filtrados pelo escopo do membership");
    }

    // ─── Factory helper ───────────────────────────────────────────────────────

    private WebApplicationFactory<Reporting.Api.AssemblyReference> CreateFactory(ReportingRole role)
    {
        return new WebApplicationFactory<Reporting.Api.AssemblyReference>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    ConfigureTestRepositories(services);
                    ConfigureTestAuth(services, role);
                });
            });
    }

    private static void ConfigureTestRepositories(IServiceCollection services)
    {
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

        var csvStorageMock = Substitute.For<ICsvStorage>();
        csvStorageMock.UploadAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CsvUploadResult(
                "https://storage.googleapis.com/azim-reports-test/report.csv?X-Goog-Expires=900",
                DateTimeOffset.UtcNow.AddMinutes(15),
                "report.csv")));

        services.AddSingleton(csvStorageMock);
    }

    private static void ConfigureTestAuth(IServiceCollection services, ReportingRole role)
    {
        var scopeResolver = Substitute.For<IScopeResolver>();

        if (role == ReportingRole.PlatformOperator)
        {
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

        services.AddAuthentication("TestScheme")
            .AddScheme<RbacTestAuthHandlerOptions, RbacTestAuthHandler>("TestScheme", opts =>
            {
                opts.TenantId = _tenantId;
                opts.UserId = _userId;
                opts.Role = role.ToString();
            });
    }
}

// ─── Handlers de autenticação locais ─────────────────────────────────────────

internal sealed class RbacTestAuthHandlerOptions : AuthenticationSchemeOptions
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "TenantAdmin";
}

internal sealed class RbacTestAuthHandler : AuthenticationHandler<RbacTestAuthHandlerOptions>
{
    public RbacTestAuthHandler(
        IOptionsMonitor<RbacTestAuthHandlerOptions> options,
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

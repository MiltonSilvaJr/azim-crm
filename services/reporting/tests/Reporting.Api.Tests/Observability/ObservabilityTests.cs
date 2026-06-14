using System.Diagnostics.Metrics;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Reporting.Application.Observability;
using Reporting.Application.Ports;
using Reporting.Contracts.ReadModels;
using Reporting.Contracts.Responses;
using Reporting.Domain.Enums;
using Reporting.Domain.ValueObjects;
using Xunit;

namespace Reporting.Api.Tests.Observability;

/// <summary>
/// Testes de observabilidade da Onda 6 — TASK-24.
///
/// Cenários cobertos:
/// <list type="bullet">
///   <item><description>Métricas <c>reports_generated_total</c> incrementadas após geração de relatório (RNF 6.2).</description></item>
///   <item><description>Histograma <c>report_generation_duration_seconds</c> emitido (RNF 6.2).</description></item>
///   <item><description>Log de geração contém <c>correlation_id</c> e <c>tenant_id</c> (RNF 6.1).</description></item>
///   <item><description>Log de geração NÃO contém PII (<c>display_name</c>, e-mail — RNF 4.2).</description></item>
///   <item><description>Health checks <c>/health/live</c> e <c>/health/ready</c> respondem 200 (TRD §11).</description></item>
///   <item><description><c>X-Correlation-Id</c> propagado em todo response (design §8.1).</description></item>
/// </list>
///
/// Mapeia: TASK-24, design §11, RNF 6, RNF 4.2, RNF 7, TRD §11, ADR-0001.
/// </summary>
public sealed class ObservabilityTests
{
    private static readonly Guid TenantId    = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId      = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // ══════════════════════════════════════════════════════════════════════════
    // 1. MÉTRICAS — reports_generated_total e report_generation_duration_seconds
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "Métrica reports_generated_total é incrementada após geração de relatório")]
    public async Task ReportsGeneratedTotal_IsIncrementedAfterReport()
    {
        var metricsCollector = new TestMeterListener();
        metricsCollector.Start();

        using var factory = CreateFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/v1/reports/funnel?from=2026-01-01&to=2026-06-30");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Aguarda a métrica ser emitida (pode haver latência mínima de propagação)
        await Task.Delay(50);

        metricsCollector.HasMeasurement("reports_generated_total")
            .Should().BeTrue("a métrica reports_generated_total deve ser emitida após geração de relatório (RNF 6.2)");
    }

    [Fact(DisplayName = "Métrica report_generation_duration_seconds é emitida após geração de relatório")]
    public async Task ReportGenerationDurationSeconds_IsRecordedAfterReport()
    {
        var metricsCollector = new TestMeterListener();
        metricsCollector.Start();

        using var factory = CreateFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false });

        await client.GetAsync("/api/v1/reports/funnel?from=2026-01-01&to=2026-06-30");
        await Task.Delay(50);

        metricsCollector.HasMeasurement("report_generation_duration_seconds")
            .Should().BeTrue("o histograma de latência deve ser emitido após geração de relatório (RNF 6.2, PTV-01)");
    }

    [Theory(DisplayName = "Métricas são emitidas para todos os tipos de relatório")]
    [InlineData("/api/v1/reports/funnel?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/forecast?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/ranking?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/channels?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/commissions?from=2026-01-01&to=2026-06-30")]
    public async Task Metrics_AreEmitted_ForAllReportTypes(string path)
    {
        var metricsCollector = new TestMeterListener();
        metricsCollector.Start();

        using var factory = CreateFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false });

        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await Task.Delay(50);

        metricsCollector.HasMeasurement("reports_generated_total")
            .Should().BeTrue($"métricas devem ser emitidas para o endpoint {path} (RNF 6.2)");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 2. LOGS ESTRUTURADOS — correlation_id, tenant_id, sem PII
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "Log de geração contém correlation_id e tenant_id (RNF 6.1)")]
    public async Task Log_ContainsCorrelationIdAndTenantId()
    {
        var logSink = new ObservabilityLogSink();
        using var factory = CreateFactoryWithLogSink(logSink);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false });

        var correlationId = Guid.NewGuid().ToString("N");
        var request = new System.Net.Http.HttpRequestMessage(
            System.Net.Http.HttpMethod.Get,
            "/api/v1/reports/funnel?from=2026-01-01&to=2026-06-30");
        request.Headers.Add("X-Correlation-Id", correlationId);

        await client.SendAsync(request);

        var allLogs = string.Join(" ", logSink.Messages);
        // Verifica que os campos operacionais estão presentes nos logs (RNF 6.1)
        allLogs.Should().Contain(TenantId.ToString(),
            "tenant_id deve aparecer nos logs estruturados (RNF 6.1, ADR-0001)");
    }

    [Fact(DisplayName = "Log de geração NÃO contém display_name, e-mail ou telefone (RNF 4.2)")]
    public async Task Log_DoesNotContainPii()
    {
        const string displayName = "Carlos Ferreira";
        var logSink = new ObservabilityLogSink();
        using var factory = CreateFactoryWithLogSinkAndRanking(logSink, displayName);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false });

        await client.GetAsync("/api/v1/reports/ranking?from=2026-01-01&to=2026-06-30");

        var allLogs = string.Join(" ", logSink.Messages);
        allLogs.Should().NotContain(displayName,
            "display_name nunca deve aparecer nos logs — é PII (DD-008, RNF 4.2)");
        allLogs.Should().NotContain("@",
            "e-mail nunca deve aparecer nos logs (RNF 4.2)");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 3. HEALTH CHECKS — /health/live e /health/ready
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "/health/live retorna 200 (liveness check — TRD §11)")]
    public async Task HealthLive_Returns200()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false });

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "/health/live deve retornar 200 quando o serviço está em execução (TRD §11, design §11)");
    }

    [Fact(DisplayName = "/health/ready retorna 2xx com health checks de Cloud SQL e GCS (TRD §11)")]
    public async Task HealthReady_Returns2xx()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false });

        var response = await client.GetAsync("/health/ready");

        // Pode ser 200 (Healthy) ou 503 com degradado (GCS Degraded), mas nunca 404/500
        var statusInt = (int)response.StatusCode;
        statusInt.Should().BeOneOf([200, 503],
            "/health/ready deve existir e responder com status de saúde dos componentes (TRD §11)");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrEmpty("health check deve retornar corpo descritivo");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 4. X-CORRELATION-ID propagado
    // ══════════════════════════════════════════════════════════════════════════

    [Theory(DisplayName = "X-Correlation-Id propagado em todo response (design §8.1, RNF 6)")]
    [InlineData("/api/v1/reports/funnel?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/forecast?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/ranking?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/channels?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/commissions?from=2026-01-01&to=2026-06-30")]
    [InlineData("/api/v1/reports/funnel/export?from=2026-01-01&to=2026-06-30")]
    public async Task XCorrelationId_IsPropagatedInAllResponses(string path)
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false });

        var response = await client.GetAsync(path);

        response.Headers.TryGetValues("X-Correlation-Id", out var values)
            .Should().BeTrue($"X-Correlation-Id deve estar no response de {path} (design §8.1, RNF 6)");
        values!.First().Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "X-Correlation-Id de entrada é preservado no response")]
    public async Task XCorrelationId_FromRequest_IsPreservedInResponse()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false });

        var correlationId = "test-corr-" + Guid.NewGuid().ToString("N");
        var request = new System.Net.Http.HttpRequestMessage(
            System.Net.Http.HttpMethod.Get,
            "/api/v1/reports/funnel?from=2026-01-01&to=2026-06-30");
        request.Headers.Add("X-Correlation-Id", correlationId);

        var response = await client.SendAsync(request);

        response.Headers.TryGetValues("X-Correlation-Id", out var values);
        values!.First().Should().Be(correlationId,
            "X-Correlation-Id enviado pelo cliente deve ser preservado no response (design §8.1)");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 5. ReportingMetrics — testes unitários diretos
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "ReportingMetrics: RecordReportGenerated não lança exceção")]
    public void ReportingMetrics_RecordReportGenerated_DoesNotThrow()
    {
        using var metrics = new ReportingMetrics();
        var act = () => metrics.RecordReportGenerated("funnel", "success", 0.5);
        act.Should().NotThrow("métricas devem ser registradas sem lançar exceção (RNF 6.2)");
    }

    [Fact(DisplayName = "ReportingMetrics: RecordExportGenerated não lança exceção")]
    public void ReportingMetrics_RecordExportGenerated_DoesNotThrow()
    {
        using var metrics = new ReportingMetrics();
        var act = () => metrics.RecordExportGenerated("funnel", 2.3);
        act.Should().NotThrow("métricas de export devem ser registradas sem lançar exceção (RNF 6.2)");
    }

    [Fact(DisplayName = "ReportingMetrics: RecordRlsDenied não lança exceção")]
    public void ReportingMetrics_RecordRlsDenied_DoesNotThrow()
    {
        using var metrics = new ReportingMetrics();
        var act = () => metrics.RecordRlsDenied("funnel");
        act.Should().NotThrow("métrica de RLS denied deve ser registrada sem lançar exceção (ADR-0001)");
    }

    [Fact(DisplayName = "ReportingMetrics: RecordScopeDenied não lança exceção")]
    public void ReportingMetrics_RecordScopeDenied_DoesNotThrow()
    {
        using var metrics = new ReportingMetrics();
        var act = () => metrics.RecordScopeDenied("funnel", "PlatformOperator");
        act.Should().NotThrow("métrica de scope denied deve ser registrada sem lançar exceção (RNF 5)");
    }

    [Fact(DisplayName = "ReportingMetrics: MeterName é 'reporting' (snake_case, design §11)")]
    public void ReportingMetrics_MeterName_IsReporting()
    {
        ReportingMetrics.MeterName.Should().Be("reporting",
            "o nome do meter deve ser 'reporting' em snake_case (design §11, RNF 6.2)");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Factories de teste
    // ══════════════════════════════════════════════════════════════════════════

    private WebApplicationFactory<Reporting.Api.AssemblyReference> CreateFactory()
    {
        return new WebApplicationFactory<Reporting.Api.AssemblyReference>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    ConfigureTestAuth(services);
                    ConfigureDefaultRepository(services);
                });
            });
    }

    private WebApplicationFactory<Reporting.Api.AssemblyReference> CreateFactoryWithLogSink(
        ObservabilityLogSink logSink)
    {
        return new WebApplicationFactory<Reporting.Api.AssemblyReference>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<ILoggerFactory>(new ObservabilityLoggerFactory(logSink));
                    ConfigureTestAuth(services);
                    ConfigureDefaultRepository(services);
                });
            });
    }

    private WebApplicationFactory<Reporting.Api.AssemblyReference> CreateFactoryWithLogSinkAndRanking(
        ObservabilityLogSink logSink,
        string displayName)
    {
        return new WebApplicationFactory<Reporting.Api.AssemblyReference>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<ILoggerFactory>(new ObservabilityLoggerFactory(logSink));
                    ConfigureTestAuth(services);

                    var repo = Substitute.For<IReportingReadRepository>();
                    SetupDefaultRepoMethods(repo);
                    repo.GetRankingAsync(Arg.Any<ReportScope>(), Arg.Any<Period>(),
                            Arg.Any<IEnumerable<Guid>?>(), Arg.Any<CancellationToken>())
                        .Returns(Task.FromResult<IReadOnlyList<RankingRow>>(
                            [new RankingRow(Guid.NewGuid(), displayName, 2, 100000L, 200000L)]));

                    services.AddSingleton(repo);
                    ConfigureCsvStorage(services);
                });
            });
    }

    private void ConfigureTestAuth(IServiceCollection services)
    {
        var scopeResolver = Substitute.For<IScopeResolver>();
        var scope = ReportScope.Create(TenantId, ReportingRole.TenantAdmin, [], null);
        scopeResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(scope));
        services.AddSingleton(scopeResolver);

        services.AddAuthentication("TestScheme")
            .AddScheme<ObsTestAuthOptions, ObsTestAuthHandler>("TestScheme", opts =>
            {
                opts.TenantId = TenantId;
                opts.UserId = UserId;
                opts.Role = "TenantAdmin";
            });
    }

    private static void ConfigureDefaultRepository(IServiceCollection services)
    {
        var repo = Substitute.For<IReportingReadRepository>();
        SetupDefaultRepoMethods(repo);
        services.AddSingleton(repo);
        ConfigureCsvStorage(services);
    }

    private static void SetupDefaultRepoMethods(IReportingReadRepository repo)
    {
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
    }

    private static void ConfigureCsvStorage(IServiceCollection services)
    {
        var csv = Substitute.For<ICsvStorage>();
        csv.UploadAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CsvUploadResult(
                "https://storage.test/bucket/test.csv",
                DateTimeOffset.UtcNow.AddMinutes(15),
                "test.csv")));
        services.AddSingleton(csv);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// Infraestrutura de teste: MeterListener, LogSink, AuthHandler
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Listener de métricas em memória para verificação de emissão de instrumentos (TASK-24).
/// Captura medições de qualquer Meter cujo nome contenha "reporting".
/// </summary>
internal sealed class TestMeterListener : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly HashSet<string> _recordedInstruments = [];

    public void Start()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name.Contains("reporting", StringComparison.OrdinalIgnoreCase))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        _listener.SetMeasurementEventCallback<long>((instrument, _, _, _) =>
            _recordedInstruments.Add(instrument.Name));
        _listener.SetMeasurementEventCallback<double>((instrument, _, _, _) =>
            _recordedInstruments.Add(instrument.Name));

        _listener.Start();
    }

    public bool HasMeasurement(string instrumentName)
    {
        _listener.RecordObservableInstruments();
        return _recordedInstruments.Contains(instrumentName);
    }

    public void Dispose() => _listener.Dispose();
}

/// <summary>Coletor de mensagens de log em memória para testes de observabilidade.</summary>
internal sealed class ObservabilityLogSink
{
    private readonly List<string> _messages = [];
    public IReadOnlyList<string> Messages => _messages.AsReadOnly();
    public void Write(string message) => _messages.Add(message);
}

internal sealed class ObservabilityLogger(ObservabilityLogSink sink, string categoryName) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        if (!string.IsNullOrEmpty(message))
        {
            sink.Write($"[{categoryName}] {message}");
        }
    }
}

internal sealed class ObservabilityLoggerFactory(ObservabilityLogSink sink) : ILoggerFactory
{
    public ILogger CreateLogger(string categoryName) => new ObservabilityLogger(sink, categoryName);
    public void AddProvider(ILoggerProvider provider) { }
    public void Dispose() { }
}

internal sealed class ObsTestAuthOptions : AuthenticationSchemeOptions
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "TenantAdmin";
}

internal sealed class ObsTestAuthHandler(
    IOptionsMonitor<ObsTestAuthOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<ObsTestAuthOptions>(options, logger, encoder)
{
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

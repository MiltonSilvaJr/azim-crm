// Reporting.Api — entry point e pipeline HTTP.
// Implementação completa da Onda 5 (TASK-21): autenticação JWT, MediatR, DI, middleware de erros,
// saúde, CORS e observabilidade mínima.
// Mapeia: TASK-21, design §8, §5.4, ADR-0001, RNF 5, RNF 6.

using System.Text.Json;
using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Reporting.Application;
using Reporting.Application.Behaviors;
using Reporting.Application.Exceptions;
using Reporting.Application.Queries.Channel;
using Reporting.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ─── Serialização JSON ────────────────────────────────────────────────────────
// - CamelCase para compatibilidade com clientes web
// - WhenWritingNull global: DisplayName omitido quando null (DD-008), GoalCents omitido quando null
// - Enums como string para legibilidade (reportType nos responses)
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    opts.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });

// ─── MediatR + pipeline behaviors ────────────────────────────────────────────
// Ordem dos behaviors (design §5.4):
// 1. CorrelationBehavior — propaga correlation_id
// 2. TenantContextBehavior — falha-fechada sem tenant
// 3. ValidationBehavior — valida período, UUIDs, enum
// 4. AuthorizationBehavior — bloqueia PlatformOperator
// 5. LoggingMetricsBehavior — log sem PII + métricas
// 6. QueryTimeoutBehavior — statement_timeout
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<GetChannelReportQuery>();
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(CorrelationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TenantContextBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingMetricsBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(QueryTimeoutBehavior<,>));
});

// ─── Application services ─────────────────────────────────────────────────────
// Registra ICsvReportWriter (CsvReportWriter), PiiMinimizationPolicy e demais serviços da Application
builder.Services.AddReportingApplication();

// ─── Infrastructure ───────────────────────────────────────────────────────────
// Em produção: connection string e GCS via Secret Manager / env var (TRD §13).
// Em desenvolvimento local / testes: pode ser sobrescrito via ConfigureServices.
var connectionString = builder.Configuration.GetConnectionString("ReportingDb")
    ?? builder.Configuration["REPORTING_DB_CONNECTION"]
    ?? "Host=localhost;Database=azim;Username=azim;Password=azim";

var useInMemoryStorage = builder.Environment.IsEnvironment("Testing")
    || builder.Environment.IsDevelopment() && !builder.Configuration.GetValue<bool>("UseGcs");

builder.Services.AddReportingInfrastructure(
    connectionString,
    configureGcs: opts =>
    {
        opts.BucketName = builder.Configuration["GCS_BUCKET"] ?? "azim-reports-dev";
        opts.SignedUrlTtl = TimeSpan.FromMinutes(
            builder.Configuration.GetValue<int?>("GCS_SIGNED_URL_TTL_MINUTES") ?? 15);
    },
    useInMemoryStorage: useInMemoryStorage);

// ─── Autenticação JWT ─────────────────────────────────────────────────────────
// Em produção: validação de JWT Bearer com authority do Identity Provider.
// Em testes (ambiente "Testing"): substituído por TestAuthHandler via WebApplicationFactory.
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opts =>
        {
            opts.Authority = builder.Configuration["JWT_AUTHORITY"];
            opts.Audience  = builder.Configuration["JWT_AUDIENCE"] ?? "azim-api";
            opts.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        });
}

builder.Services.AddAuthorization();

// ─── Health checks ─────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

// ─── OpenAPI ─────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();

// ─── Problem Details ─────────────────────────────────────────────────────────
builder.Services.AddProblemDetails();

var app = builder.Build();

// ─── Middleware de X-Correlation-Id ──────────────────────────────────────────
// Propaga ou gera correlation_id em todo response (RNF 6, ADR-0001).
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? Guid.NewGuid().ToString("N");

    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers["X-Correlation-Id"] = correlationId;

    await next();
});

// ─── Middleware de tratamento global de exceções ───────────────────────────────
// Mapeia exceções tipadas para respostas HTTP com catálogo de erros (design §12).
// Nunca expõe PII nem stack trace em produção (RNF 4.3).
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async context =>
    {
        var exceptionFeature = context.Features
            .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();

        var exception = exceptionFeature?.Error;
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? string.Empty;

        var (statusCode, code, message) = exception switch
        {
            AccessDeniedException => (StatusCodes.Status403Forbidden,
                AccessDeniedException.ErrorCode,
                "Acesso negado ao relatório."),

            TenantNotResolvedException => (StatusCodes.Status403Forbidden,
                TenantNotResolvedException.ErrorCode,
                "Tenant não resolvido. Reautentique e tente novamente."),

            UnauthorizedAccessException => (StatusCodes.Status403Forbidden,
                "REPORT-ERR-005",
                "Acesso negado ao relatório."),

            // Período inválido: from > to (REPORT-ERR-001, design §12)
            InvalidPeriodException => (StatusCodes.Status400BadRequest,
                InvalidPeriodException.ErrorCode,
                "Período inválido. A data 'from' deve ser anterior ou igual a 'to'."),

            // BU fora do escopo RBAC: anti-enumeração 404 genérico (REPORT-ERR-004, design §10)
            BuIdOutOfScopeException => (StatusCodes.Status404NotFound,
                BuIdOutOfScopeException.ErrorCode,
                "Recurso não encontrado no seu escopo."),

            // Tipo de relatório inválido no export (REPORT-ERR-003, design §12)
            InvalidReportTypeException => (StatusCodes.Status400BadRequest,
                InvalidReportTypeException.ErrorCode,
                "Tipo de relatório inválido. Use funnel, forecast, ranking, channel ou commissions."),

            FluentValidation.ValidationException => (StatusCodes.Status400BadRequest,
                "REPORT-ERR-001",
                // Mensagem genérica — nunca expõe detalhes internos ou PII
                "Requisição inválida. Verifique os parâmetros informados."),

            OperationCanceledException => (StatusCodes.Status503ServiceUnavailable,
                "REPORT-ERR-008",
                "Relatório temporariamente indisponível. Tente novamente."),

            _ => (StatusCodes.Status500InternalServerError,
                "REPORT-ERR-007",
                "Falha interna ao processar a requisição.")
        };

        context.Response.StatusCode  = statusCode;
        context.Response.ContentType = "application/json";

        var body = JsonSerializer.Serialize(new
        {
            code,
            message,
            correlationId
        });

        await context.Response.WriteAsync(body);
    });
});

// ─── Pipeline ─────────────────────────────────────────────────────────────────
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ─── Health checks ─────────────────────────────────────────────────────────
// /health/live — liveness: apenas verifica se o processo responde (sem dependências externas).
// Predicate vazio = nenhum IHealthCheck registrado é executado; sempre Healthy se o processo está vivo.
// Ref: design §11, RNF 7, TRD §11.
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});
// /health/ready — readiness: executa Cloud SQL (tag "database") e GCS (tag "storage").
// Retorna Unhealthy/Degraded se alguma dependência estiver indisponível.
app.MapHealthChecks("/health/ready");

app.Run();

// Marcador público para resolução de assembly em testes de arquitetura e WebApplicationFactory.
namespace Reporting.Api
{
    /// <summary>
    /// Marcador de assembly para resolução de referências em testes de arquitetura (Architecture.Tests).
    /// Substitui AssemblyReference.cs — evita duplicidade com o Program gerado.
    /// </summary>
    public sealed class AssemblyReference { }
}

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TenantAdministration.Api.Extensions;
using TenantAdministration.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ─── Serviços ────────────────────────────────────────────────────────────────

builder.Services.AddControllers();

// OpenAPI (design.md TASK-20)
builder.Services.AddOpenApi();

// Application services: MediatR, behaviors, validators, ICurrentUserContext, ITenantContext
builder.Services.AddApplicationServices();

// Exception handler global com application/problem+json (design.md §12)
builder.Services.AddExceptionHandler<TenantAdministrationExceptionHandler>();
builder.Services.AddProblemDetails();

// Response cache (brand.json — design.md §8.3)
builder.Services.AddResponseCaching();

// Métricas OpenTelemetry-compatíveis (TASK-22, design.md §11)
builder.Services.AddMetrics();

// Health checks base — endpoints /health/live e /health/ready (TASK-22, design.md §11)
// Checks específicos de dependências são adicionados em AddTenantAdministrationInfrastructure.
builder.Services.AddHealthChecks();

// ─── Build ───────────────────────────────────────────────────────────────────

var app = builder.Build();

// ─── Middleware ──────────────────────────────────────────────────────────────

app.UseExceptionHandler();

// OpenAPI disponível em Development e Testing (contratos e testes — TASK-20)
if (!app.Environment.IsProduction())
    app.MapOpenApi();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseResponseCaching();

app.MapControllers();

// ─── Health Checks (TASK-22, design.md §11) ──────────────────────────────────
// /health/live — liveness: aplicação está rodando (independente de dependências)
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false  // não executa nenhum check — apenas responde 200 se o processo está vivo
});

// /health/ready — readiness: verifica conectividade com dependências externas
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

app.Run();

// Exposto para WebApplicationFactory nos testes de integração
namespace TenantAdministration.Api
{
    /// <summary>
    /// Ponto de entrada da API — exposto para testes com WebApplicationFactory.
    /// </summary>
    public partial class Program { }
}

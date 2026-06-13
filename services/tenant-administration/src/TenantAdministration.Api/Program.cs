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

app.Run();

// Exposto para WebApplicationFactory nos testes de integração
namespace TenantAdministration.Api
{
    /// <summary>
    /// Ponto de entrada da API — exposto para testes com WebApplicationFactory.
    /// </summary>
    public partial class Program { }
}

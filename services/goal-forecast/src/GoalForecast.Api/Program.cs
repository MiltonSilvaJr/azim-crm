// GoalForecast.Api — ponto de entrada do slice no monólito modular azim-api.
// Onda 5: configuração completa de DI, autenticação JWT, controllers e OpenAPI.

using System.Security.Claims;
using FluentValidation;
using GoalForecast.Api.Filters;
using GoalForecast.Application.Behaviors;
using GoalForecast.Application.Ports;
using GoalForecast.Infrastructure.Membership;
using GoalForecast.Infrastructure.Outbox;
using GoalForecast.Infrastructure.Persistence;
using GoalForecast.Infrastructure.Pipeline;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── MediatR ──────────────────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(
        typeof(GoalForecast.Application.AssemblyReference).Assembly);
});

// ── Pipeline behaviors (na ordem correta — design §5.4) ───────────────────────
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TenantContextBehavior<,>));
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(GoalAuthorizationBehavior<,>));
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));

// ── FluentValidation ─────────────────────────────────────────────────────────
builder.Services.AddValidatorsFromAssembly(
    typeof(GoalForecast.Application.AssemblyReference).Assembly);

// ── Autenticação JWT (produção; substituída por FakeAuthHandler nos testes) ───
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.Audience = builder.Configuration["Auth:Audience"];
    });

// ── Autorização ───────────────────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    // Política para endpoint interno do Digest (mTLS/service-scope) — design §8.6
    options.AddPolicy("ServiceScope", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("role", "ServiceScope"));
});

// ── HTTP Context Accessor (necessário para extrair tenant do request) ─────────
builder.Services.AddHttpContextAccessor();

// ── GoalForecastDbContext com tenant dinâmico por request ─────────────────────
// O tenant_id vem do principal autenticado — nunca de configuração estática (ADR-0001).
builder.Services.AddScoped(sp =>
{
    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
    var tenantClaim = httpContextAccessor.HttpContext?.User
        .FindFirstValue("tenantId");
    var tenantId = tenantClaim is not null
        ? Guid.Parse(tenantClaim)
        : Guid.Empty;

    var connStr = builder.Configuration.GetConnectionString("GoalForecast")
        ?? "Host=localhost;Database=goalforecast;Username=postgres";

    var optionsBuilder = new DbContextOptionsBuilder<GoalForecastDbContext>();
    optionsBuilder.UseNpgsql(connStr);

    return new GoalForecastDbContext(optionsBuilder.Options, tenantId);
});

// ── Portas de infraestrutura ──────────────────────────────────────────────────
builder.Services.AddScoped<IGoalRepository, GoalRepository>();
builder.Services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();

builder.Services.AddScoped<IBuMembershipReader>(sp =>
    new BuMembershipReader(
        builder.Configuration.GetConnectionString("GoalForecast")
            ?? "Host=localhost;Database=goalforecast;Username=postgres",
        sp.GetRequiredService<ILogger<BuMembershipReader>>()));

// ForecastViewSource: stub por padrão — substituir em produção pela implementação real (DD-005)
builder.Services.AddScoped<IForecastViewSource, StubForecastViewSource>();
builder.Services.AddScoped<IPipelineForecastReader, PipelineForecastReader>();

// ── Controllers + JSON ────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // Preservar long como número inteiro — evitar serialização como double (RISK-GOAL-05)
        o.JsonSerializerOptions.NumberHandling =
            System.Text.Json.Serialization.JsonNumberHandling.Strict;
    });

// ── OpenAPI ───────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// ── Health checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseAuthentication();
app.UseAuthorization();

// Middleware de exceções de domínio → ProblemDetails com código GF-ERR-* (design §12)
app.UseMiddleware<GoalForecastExceptionMiddleware>();

app.MapControllers();
app.MapHealthChecks("/health");

await app.RunAsync();

// Marcador de partial para que WebApplicationFactory acesse o tipo
namespace GoalForecast.Api
{
    public partial class Program { }
}

using Authentication.Api.Middleware;
using Authentication.Application.Ports;
using Authentication.Application.Services;
using Authentication.Infrastructure.Audit;
using Authentication.Infrastructure.Directory;
using Authentication.Infrastructure.Email;
using Authentication.Infrastructure.Firebase;
using Authentication.Infrastructure.RateLimiting;

// =========================================================================
// Authentication.Api — ponto de entrada do módulo BC-12.
//
// Pipeline de middlewares (design.md § 5.4, ordem obrigatória):
//   1. CorrelationMiddleware   — garante correlationId por requisição (RNF 4.1)
//   2. TenantResolutionMiddleware — resolve slug → tenant_id (Req 1.5, DEC-006)
//   3. RateLimitingMiddleware  — limita por IP e tenant (RNF 8)
//   4. AuthenticationMiddleware — valida token e injeta AuthContext (Req 4)
//   5. Controllers / Endpoints — lógica de negócio
//
// Regra de dependência (DD-001):
//   Api → Application, Contracts, Infrastructure (wiring DI apenas)
//   Nenhum tipo do Firebase Admin SDK exposto aqui.
//   Nenhum identity_uid exposto aqui.
// =========================================================================

var builder = WebApplication.CreateBuilder(args);

// =========================================================================
// DI — Portas de saída e serviços de aplicação
// =========================================================================

// Cache em memória para AuthContextComposer (RNF 2.3)
builder.Services.AddMemoryCache();

// Options de password reset (delay configurável — RISK-AUTH-05)
builder.Services.Configure<PasswordResetOptions>(
    builder.Configuration.GetSection("PasswordReset"));

// Implementações stub/no-op que podem ser sobrescritas em testes
// (os adapters reais requerem banco/Redis/Firebase — configurados via DI extensions)
builder.Services.AddScoped<ITenantDirectory, NullTenantDirectory>();
builder.Services.AddScoped<IUserDirectory, NullUserDirectory>();
builder.Services.AddScoped<IIdentityProvider, NullIdentityProvider>();
builder.Services.AddScoped<IRateLimiter, RedisRateLimiter>();
builder.Services.AddScoped<IEmailSender, NoOpEmailSender>();
builder.Services.AddScoped<IAuditEventEmitter, NoOpAuditEventEmitter>();

// Serviços de aplicação
builder.Services.AddScoped<SessionTokenValidator>();
builder.Services.AddScoped<AuthContextComposer>();
builder.Services.AddScoped<SessionRevocationService>();
builder.Services.AddScoped<InviteActivationService>();
builder.Services.AddScoped<PasswordResetService>();

// =========================================================================
// DI — ASP.NET Core
// =========================================================================
builder.Services.AddControllers();

var app = builder.Build();

// =========================================================================
// Pipeline de middlewares (ordem obrigatória — design.md § 5.4)
// =========================================================================

// 1. Correlation — garante correlationId em 100% dos logs (RNF 4.1)
app.UseMiddleware<CorrelationMiddleware>();

// 2. TenantResolution — resolve slug → tenant_id (Req 1.5)
app.UseMiddleware<TenantResolutionMiddleware>();

// 3. RateLimiting — limita por IP e tenant (RNF 8)
app.UseMiddleware<RateLimitingMiddleware>();

// 4. Authentication — valida token e injeta AuthContext (Req 4)
app.UseMiddleware<AuthenticationMiddleware>();

// =========================================================================
// Rotas de saúde (sem autenticação, sem resolução de tenant)
// =========================================================================
app.MapGet("/health/ping", () => Results.Ok(new { status = "ok" }));

// =========================================================================
// Controllers
// =========================================================================
app.MapControllers();

app.Run();

// Marca Program como público para WebApplicationFactory (testes de integração)
public partial class Program { }

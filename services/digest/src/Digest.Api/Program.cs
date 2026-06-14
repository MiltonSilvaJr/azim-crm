using System.Text.Json;
using Digest.Api.Endpoints;
using Digest.Api.Infrastructure;
using FluentValidation;
using Digest.Application.Abstractions;
using Digest.Application.Behaviors;
using Digest.Application.Ports;
using Digest.Application.Repositories;
using Digest.Infrastructure.Adapters;
using Digest.Infrastructure.Clock;
using Digest.Infrastructure.Messaging;
using Digest.Infrastructure.Persistence;
using Digest.Infrastructure.Rendering;
using Digest.Infrastructure.Repositories;
using Digest.Infrastructure.Security;
using Digest.Infrastructure.Token;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

// =============================================================================
// azim-digest-worker — Program.cs
// Configuração completa do pipeline ASP.NET Core (TASK-21).
// Segurança: OIDC/WIF no trigger (design §10, RNF 7.1).
// Assíncrono: trigger responde 202 antes de processar envios (RNF 8.1, DD-005).
// DI: compõe Infrastructure (repositórios, adaptadores HttpClient, portas) (TASK-21).
// =============================================================================

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuração: leitura de opções tipadas
// ---------------------------------------------------------------------------
var oidcOptions = builder.Configuration.GetSection("Oidc").Get<OidcOptions>()
    ?? new OidcOptions();

// ---------------------------------------------------------------------------
// Autenticação: OIDC/WIF — valida token do Cloud Scheduler (RNF 7.1, design §10)
// ---------------------------------------------------------------------------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Authority: endpoint OIDC do GCP (Cloud Scheduler usa google accounts)
        options.Authority = oidcOptions.Authority ?? "https://accounts.google.com";
        // Audience: URL do endpoint do worker (validação de audience do token)
        options.Audience = oidcOptions.Audience;
        // Em desenvolvimento, tolerar HTTPS inválido
        if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
        {
            options.RequireHttpsMetadata = false;
        }

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                // Log sem PII: não registrar claims, apenas evento de falha (RNF 3, DD-011)
                ctx.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>()
                    .LogWarning("Falha de autenticação OIDC no trigger — DIG-ERR-010. TraceId: {TraceId}",
                        ctx.HttpContext.TraceIdentifier);
                return Task.CompletedTask;
            }
        };
    });

// ---------------------------------------------------------------------------
// Autorização: política que verifica o email da SA do Cloud Scheduler (design §10)
// ---------------------------------------------------------------------------
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(TriggerAuthorizationPolicy.Name, policy =>
        policy.RequireAuthenticatedUser()
              .AddRequirements(new SchedulerServiceAccountRequirement(
                  oidcOptions.AuthorizedServiceAccountEmail
                  ?? "cloud-scheduler@azim-prod.iam.gserviceaccount.com")));
});
builder.Services.AddSingleton<
    Microsoft.AspNetCore.Authorization.IAuthorizationHandler,
    SchedulerServiceAccountHandler>();

// ---------------------------------------------------------------------------
// MediatR: handlers, behaviors (design §5.4)
// ---------------------------------------------------------------------------
var applicationAssembly = typeof(Digest.Application.AssemblyReference).Assembly;

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(applicationAssembly);
    // Pipeline behaviors (ordem: TenantScope → Logging → Validation → UnitOfWork)
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TenantScopeBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));
});

// ---------------------------------------------------------------------------
// FluentValidation
// ---------------------------------------------------------------------------
builder.Services.AddValidatorsFromAssembly(applicationAssembly);

// ---------------------------------------------------------------------------
// Infrastructure — Persistência: EF Core + Npgsql + RLS (TASK-14, TASK-15)
// ---------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DigestDb")
    ?? "Host=localhost;Database=azim_digest;Username=postgres;Password=postgres";

builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<TenantContext>(sp =>
    (TenantContext)sp.GetRequiredService<ITenantContext>());

builder.Services.AddDbContext<DigestDbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString, npgsql => npgsql.UseNodaTime());
    options.AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>());
});

builder.Services.AddScoped<TenantConnectionInterceptor>();

// ---------------------------------------------------------------------------
// Infrastructure — Repositórios (TASK-17)
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IEmailDigestLogRepository, EmailDigestLogRepository>();
builder.Services.AddScoped<IDigestActionTokenRepository, DigestActionTokenRepository>();
builder.Services.AddScoped<Digest.Application.Behaviors.IUnitOfWork, Digest.Infrastructure.Persistence.UnitOfWork>();

// ---------------------------------------------------------------------------
// Infrastructure — Outbox, Clock, Renderer, Token (TASK-16, TASK-18, TASK-20)
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IOutboxPublisher, OutboxPublisher>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<IEmailTemplateRenderer, EmailTemplateRenderer>();
builder.Services.AddScoped<IActionTokenFactory, ActionTokenFactory>();

// ---------------------------------------------------------------------------
// Infrastructure — Adaptadores das portas de leitura com HttpClient + Polly (TASK-19)
// ---------------------------------------------------------------------------
var internalBaseUrl = builder.Configuration["InternalApi:BaseUrl"] ?? "http://azim-api";

builder.Services.AddHttpClient<IUserDirectoryPort, DirectoryReadAdapter>(client =>
{
    client.BaseAddress = new Uri(internalBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddHttpClient<IActivityReadPort, PendenciasReadAdapter>(client =>
{
    client.BaseAddress = new Uri(internalBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddHttpClient<IOpportunityReadPort, PipelineReadAdapter>(client =>
{
    client.BaseAddress = new Uri(internalBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddHttpClient<IForecastReadPort, ForecastReadAdapter>(client =>
{
    client.BaseAddress = new Uri(internalBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddHttpClient<IUserDigestPreferencePort, DigestPreferenceReadAdapter>(client =>
{
    client.BaseAddress = new Uri(internalBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});

// ---------------------------------------------------------------------------
// Infrastructure — IEmailSender (stub de MVP — implementação real no notification-delivery)
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IEmailSender, NoOpEmailSender>();

// ---------------------------------------------------------------------------
// Api — Fan-out Pub/Sub (MVP: in-process; produção: SDK do GCP Pub/Sub)
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IPubSubFanout, InProcessFanout>();

// ---------------------------------------------------------------------------
// Health Checks: live/ready (design §8.2)
// ---------------------------------------------------------------------------
builder.Services.AddHealthChecks()
    .AddCheck("liveness", () => HealthCheckResult.Healthy("Processo vivo"), tags: ["live"])
    .AddDbContextCheck<DigestDbContext>("database", tags: ["ready"])
    .AddCheck<PubSubHealthCheck>("pubsub", tags: ["ready"])
    .AddCheck<EmailSenderHealthCheck>("email-sender", tags: ["ready"]);

// ---------------------------------------------------------------------------
// JSON options
// ---------------------------------------------------------------------------
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    opts.SerializerOptions.WriteIndented = false;
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Pipeline de middleware
// ---------------------------------------------------------------------------
app.UseAuthentication();
app.UseAuthorization();

// ---------------------------------------------------------------------------
// Health checks endpoints (design §8.2)
// ---------------------------------------------------------------------------
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
    }
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
    }
});

// ---------------------------------------------------------------------------
// Endpoints internos
// ---------------------------------------------------------------------------
app.MapGroup("/internal/digest")
    .MapDigestTriggerEndpoint()
    .MapPerTenantConsumerEndpoint();

app.Run();

// Torna Program acessível ao WebApplicationFactory nos testes de API
public partial class Program;

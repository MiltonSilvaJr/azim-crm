// ActivityManagement.Api — entry point do serviço HTTP.
// Pipeline completo: autenticação JWT, MediatR, validators, controllers, OpenAPI,
// middleware de correlação e exception handling, health checks.
// Mapeia: design §8, design §11, TASK-18, TASK-19.

using ActivityManagement.Application.Activities.Commands;
using ActivityManagement.Application.Behaviors;
using ActivityManagement.Application.Validators;
using ActivityManagement.Api.Middleware;
using ActivityManagement.Infrastructure;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Autenticação JWT (Req 13.4, rule jwt-permissions.md) ──────────────────────
var isTestEnvironment = builder.Environment.EnvironmentName == "Testing";

if (!isTestEnvironment)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority             = builder.Configuration["Auth:Authority"];
            options.Audience              = builder.Configuration["Auth:Audience"];
            options.RequireHttpsMetadata  = !builder.Environment.IsDevelopment();
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidateAudience         = true,
                ValidateLifetime         = true,
                ValidateIssuerSigningKey = true,
            };
        });
}
else
{
    // Em testes, a autenticação é substituída pelo TestAuthHandler via WebApplicationFactory
    builder.Services.AddAuthentication();
}

builder.Services.AddAuthorization();

// ── Infrastructure (repositório, DbContext, adapters, Outbox, health checks) ──
if (!isTestEnvironment)
{
    builder.Services.AddInfrastructure(builder.Configuration);
}

// ── MediatR: handlers de Application + pipeline behaviors ─────────────────────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CreateActivityCommand).Assembly);

    // Pipeline behaviors na ordem definida em design §5.4:
    // 1. Logging/correlação
    // 2. Tenant scope (falha-fechada)
    // 3. Validação FluentValidation
    // 4. Autorização RBAC
    // 5. Transaction (Outbox)
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(CorrelationLoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TenantScopeBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
});

// ── FluentValidation ──────────────────────────────────────────────────────────
builder.Services.AddValidatorsFromAssembly(typeof(CreateActivityValidator).Assembly);

// ── Controllers + OpenAPI ─────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "Activity Management API",
        Version     = "v1",
        Description = "API REST para gestão de atividades comerciais do Azim CRM (BC-04).",
    });

    // Esquema de segurança JWT
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Informe o token JWT no formato: Bearer {token}",
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer",
                },
            },
            Array.Empty<string>()
        },
    });

    // Inclui comentários XML para documentar operações
    var xmlFile = $"{typeof(ActivityManagement.Api.AssemblyReference).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

// ── Health checks ─────────────────────────────────────────────────────────────
if (!isTestEnvironment)
{
    // Health checks de PostgreSQL são registrados pelo AddInfrastructure
}
else
{
    builder.Services.AddHealthChecks();
}

// ── App pipeline ──────────────────────────────────────────────────────────────
var app = builder.Build();

// Correlation ID deve ser o primeiro middleware (antes do exception handler)
app.UseMiddleware<CorrelationIdMiddleware>();

// Exception handler global — mapeia exceções para catálogo ACT-ERR-001..011
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

if (app.Environment.IsDevelopment() || isTestEnvironment)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Activity Management API v1");
    });
}

app.UseAuthentication();
app.UseAuthorization();

// Health checks
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false, // liveness: sem verificações externas
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResultStatusCodes =
    {
        [HealthStatus.Healthy]   = StatusCodes.Status200OK,
        [HealthStatus.Degraded]  = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
    },
});

app.MapControllers();

await app.RunAsync();

// Torna Program acessível para WebApplicationFactory nos testes de API
public partial class Program { }

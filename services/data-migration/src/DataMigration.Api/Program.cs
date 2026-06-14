// DataMigration.Api — entry point do módulo data-migration.
// Ondas 1-4 proveram Domain, Application e Infrastructure.
// Onda 5 adiciona MigrationController, DI completa e middleware de erros.
//
// Rastreia: design §3, §8, §10, §12, TASK-21, TASK-22, TASK-23.

using DataMigration.Application.Behaviors;
using DataMigration.Application.Ports;
using DataMigration.Api.Middleware;
using DataMigration.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// =========================================================================
// Autenticação JWT (design §10 — GCP Identity Platform)
// Em ambiente de teste, o TestAuthHandler substitui este handler via
// WebApplicationFactory.ConfigureServices().
// =========================================================================
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.Audience = builder.Configuration["Auth:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment()
            && !builder.Environment.IsEnvironment("Testing");
    });

builder.Services.AddAuthorization();

// =========================================================================
// HttpContextAccessor — necessário para ICurrentTenantContext
// =========================================================================
builder.Services.AddHttpContextAccessor();

// =========================================================================
// ICurrentTenantContext — resolve tenant do JWT em produção
// =========================================================================
builder.Services.AddScoped<ICurrentTenantContext, JwtTenantContext>();

// =========================================================================
// Infrastructure (DbContext, Repositório, Adaptadores, Parser, etc.)
// =========================================================================
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDataMigrationInfrastructure(builder.Configuration);
}

// =========================================================================
// MediatR — pipeline de commands/queries com behaviors (design §5.4)
// =========================================================================
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(
        typeof(DataMigration.Application.AssemblyReference).Assembly);

    // Ordem dos behaviors: Tenant → Auth → FeatureFlag → Validation → PiiSafe → Idempotency
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TenantContextBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(FeatureFlagBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PiiSafeLoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));
});

// =========================================================================
// Controllers com ProblemDetails (design §12)
// =========================================================================
builder.Services.AddControllers();
builder.Services.AddProblemDetails();

// =========================================================================
// OpenAPI / Swagger (design §8)
// =========================================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Azim CRM — Data Migration API",
        Version = "v1",
        Description = "API REST do módulo data-migration (BC-09). " +
            "Endpoints de upload, dry-run, triagem e import transacional.",
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT via GCP Identity Platform.",
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            },
            []
        },
    });
});

var app = builder.Build();

// =========================================================================
// Middleware pipeline
// =========================================================================

// Exception handler antes de tudo
app.UseMiddleware<MigrationExceptionMiddleware>();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// =========================================================================
// Implementação de ICurrentTenantContext via JWT (produção)
// =========================================================================

/// <summary>
/// Implementação de <see cref="ICurrentTenantContext"/> que resolve tenant e papel
/// a partir dos claims do JWT (GCP Identity Platform).
///
/// Claims esperados (configurados no Identity Platform):
///   - <c>tenant_id</c>: UUID do tenant.
///   - <c>user_id</c> ou <c>sub</c>: UUID do usuário.
///   - <c>role</c> ou <c>custom:role</c>: papel (PlatformOperator | TenantAdmin).
///
/// Rastreia: design §10, ADR-0001, TASK-21.
/// </summary>
internal sealed class JwtTenantContext : ICurrentTenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public JwtTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? TenantId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User
                .FindFirst("tenant_id")?.Value;

            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public Guid? UserId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User
                .FindFirst("user_id")?.Value
                ?? _httpContextAccessor.HttpContext?.User
                .FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public string? Role =>
        _httpContextAccessor.HttpContext?.User
            .FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
}

// Torna o Program acessível para WebApplicationFactory
public partial class Program { }

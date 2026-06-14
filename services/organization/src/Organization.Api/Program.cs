// Organization.Api — Ponto de entrada do módulo organization.
// Composição raiz: DI, middlewares, autenticação JWT, OpenAPI.

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Organization.Api.DependencyInjection;
using Organization.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ── Application Layer (MediatR, behaviors, validators) ───────────────────────
builder.Services.AddOrganizationApplication();

// ── Infrastructure Layer (DbContext, repositórios, Redis, adapters) ──────────
builder.Services.AddOrganizationInfrastructure(builder.Configuration);

// ── JWT Tenant Context (ITenantContext por request) ──────────────────────────
builder.Services.AddJwtTenantContext();

// ── Autenticação JWT ─────────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Jwt:Authority"];
        options.Audience = builder.Configuration["Jwt:Audience"] ?? "organization-api";
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();

// ── Controllers ──────────────────────────────────────────────────────────────
builder.Services.AddControllers();

// ── OpenAPI / Swagger ─────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Organization API",
        Version = "v1",
        Description = "API REST do módulo Organization do Azim CRM. " +
                      "Gerencia Business Units, usuários, memberships, convites e configuração de pipeline.",
    });

    // Segurança JWT no Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Informe o token JWT no formato: Bearer {token}",
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            Array.Empty<string>()
        },
    });

    // Inclui XML comments (atributos <summary> dos controllers)
    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

// ── Health Checks ────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────

// Exception handler antes de tudo — captura exceções de qualquer middleware abaixo
app.UseOrganizationExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Organization API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseAuthentication();
app.UseAuthorization();

// Popula ITenantContext a partir dos claims JWT
app.UseJwtTenantContext();

app.MapControllers();

// Health checks — live (sem dependências externas) e ready (Postgres + Redis)
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

await app.RunAsync();

// Torna Program acessível ao WebApplicationFactory nos testes de API
public partial class Program { }

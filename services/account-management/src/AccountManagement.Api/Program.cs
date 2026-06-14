using AccountManagement.Application.DependencyInjection;
using AccountManagement.Api.Middleware;
using AccountManagement.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;

// =============================================================================
// Azim CRM — account-management API
// Pipeline HTTP, controllers REST, middleware e DI completo.
// Mapeia: TASK-13..TASK-15, design §8, design §10, design §11.
// =============================================================================

var builder = WebApplication.CreateBuilder(args);

// =====================================================================
// Authentication — JWT Bearer (GCP Identity Platform)
// Em produção usa validação de assinatura e audiência (rule jwt-permissions.md)
// =====================================================================
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.Audience = builder.Configuration["Auth:Audience"];
        // Em ambiente de desenvolvimento/teste sem Authority configurada, permite continuar
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });

builder.Services.AddAuthorization();

// =====================================================================
// MVC + Controllers
// =====================================================================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// =====================================================================
// Application layer (MediatR, behaviors, validators, contextos)
// =====================================================================
builder.Services.AddAccountManagementApplication();

// =====================================================================
// Infrastructure layer (DbContext, repositórios, adapters, Outbox)
// Configuração opcional — em testes a infra é substituída por mocks
// =====================================================================
var connectionString = builder.Configuration.GetConnectionString("AccountManagement");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddAccountManagementInfrastructure(connectionString);
}

// =====================================================================
// OpenAPI / Swagger (TASK-15)
// =====================================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Account Management API",
        Version = "v1",
        Description = "API de gestão de contas e contatos do Azim CRM. " +
                      "Todos os dados são isolados por tenant (multi-tenancy).",
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Bearer token (GCP Identity Platform — rule jwt-permissions.md)",
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                    { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            },
            []
        },
    });

    // Inclui comentários XML dos controllers para documentação dos endpoints
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

// =====================================================================
// Health checks
// =====================================================================
builder.Services.AddHealthChecks();

var app = builder.Build();

// =====================================================================
// Middleware pipeline — ordem importa (design §5.4)
// =====================================================================

// 1. ExceptionHandling — deve ser o primeiro para capturar todas as exceções
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 2. CorrelationId — antes da autenticação para que esteja presente em toda resposta
app.UseMiddleware<CorrelationIdMiddleware>();

// 3. OpenAPI em desenvolvimento
if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Test")
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Account Management API v1");
    });
}

// 4. Autenticação e autorização
app.UseAuthentication();
app.UseAuthorization();

// 5. Controllers
app.MapControllers();

// 6. Health checks
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.Run();

// Marcador de tipo exposto para WebApplicationFactory nos testes de API (TASK-13..15).
namespace AccountManagement.Api
{
    /// <summary>
    /// Ponto de entrada do serviço account-management.
    /// Expõe o tipo <see cref="Program"/> para permitir que
    /// <c>WebApplicationFactory&lt;Program&gt;</c> nos testes de Api localize o assembly
    /// sem necessitar de referência direta ao projeto.
    /// </summary>
    public partial class Program { }
}

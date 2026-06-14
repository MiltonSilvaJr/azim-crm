using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpportunityPipeline.Api.Auth;
using OpportunityPipeline.Api.Filters;
using OpportunityPipeline.Api.Middleware;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Application.Opportunities.Commands;
using OpportunityPipeline.Infrastructure;

// ============================================================================
// Construção da aplicação
// ============================================================================

var builder = WebApplication.CreateBuilder(args);

// ---- Controllers -----------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
    });

// ---- OpenAPI / Swashbuckle -------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Opportunity Pipeline API",
        Version = "v1",
        Description = "API REST do módulo opportunity-pipeline (Azim CRM). Money em centavos inteiros (long). Erros padronizados com OP-ERR-*."
    });

    // Header Idempotency-Key documentado em todos os endpoints de escrita
    c.OperationFilter<IdempotencyKeyOperationFilter>();

    // Exemplos de ProblemDetails com OP-ERR-*
    c.OperationFilter<OpErrorResponseFilter>();

    // Inclui comentários XML (GenerateDocumentationFile = true)
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);

    // Esquema de segurança JWT
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT do usuário (rule jwt-authentication). Formato: Bearer {token}"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
    });
});

// ---- Autenticação JWT (rule jwt-authentication) ----------------------------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        var jwtSection = builder.Configuration.GetSection("Jwt");
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"] ?? "azim-crm",
            ValidAudience = jwtSection["Audience"] ?? "azim-api",
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSection["Key"] ?? "azim-dev-secret-key-not-for-production")),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

// ---- Autenticação de serviço interno (mTLS / service-identity) -------------
// Política separada da autenticação JWT de usuário.
// Em produção: usar certificados mTLS com Google Cloud identity tokens.
// Em testes: header X-Service-Identity simulado (ServiceIdentityAuthHandler).
builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, ServiceIdentityAuthHandler>(
        ServiceIdentityAuthHandler.SchemeName,
        _ => { });

// ---- Políticas de autorização RBAC -----------------------------------------
builder.Services.AddAuthorization(opts =>
{
    // Políticas por capacidade (design §10 — matriz papel × capacidade)
    opts.AddPolicy("WriteOpportunity", policy =>
        policy.RequireClaim("role",
            nameof(UserRole.Vendedor),
            nameof(UserRole.GestorBU),
            nameof(UserRole.TenantAdmin)));

    opts.AddPolicy("ReadOpportunity", policy =>
        policy.RequireClaim("role",
            nameof(UserRole.Vendedor),
            nameof(UserRole.GestorBU),
            nameof(UserRole.TenantAdmin),
            nameof(UserRole.Viewer)));

    opts.AddPolicy("ReopenOpportunity", policy =>
        policy.RequireClaim("role",
            nameof(UserRole.GestorBU),
            nameof(UserRole.TenantAdmin)));

    opts.AddPolicy("ServiceIdentity", policy =>
        policy.RequireAuthenticatedUser()
              .AddAuthenticationSchemes(ServiceIdentityAuthHandler.SchemeName));
});

// ---- MediatR (Application) -------------------------------------------------
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(OpportunityPipeline.Application.AssemblyReference).Assembly);

    // Pipeline behaviors na ordem correta (design §5.4):
    // Logging → Tenant → Rbac → Idempotency → Validation → Transaction
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(TenantBehavior<,>));
    cfg.AddOpenBehavior(typeof(RbacBehavior<,>));
    cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
});

// ---- FluentValidation ------------------------------------------------------
builder.Services.AddValidatorsFromAssembly(
    typeof(OpportunityPipeline.Application.AssemblyReference).Assembly);

// ---- TenantContext (scoped — ciclo de vida da request) ---------------------
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantResolver, HttpContextTenantResolver>();

// ---- WinOpportunityOptions (VAL-07 / DD-007) --------------------------------
builder.Services.Configure<WinOpportunityOptions>(
    builder.Configuration.GetSection("WinOpportunity"));
builder.Services.AddSingleton<CommissionOnWinChecker>();

// ---- Infrastructure services -----------------------------------------------
builder.Services.AddInfrastructure(builder.Configuration);

// ---- Health Checks ---------------------------------------------------------
builder.Services.AddHealthChecks();

// ---- ProblemDetails --------------------------------------------------------
builder.Services.AddProblemDetails();

// ============================================================================
// Pipeline HTTP
// ============================================================================

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Opportunity Pipeline v1");
        c.RoutePrefix = "swagger";
    });
}

// Middleware de tratamento de erros → ProblemDetails com OP-ERR-*
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Necessário para WebApplicationFactory em testes de integração
public partial class Program { }

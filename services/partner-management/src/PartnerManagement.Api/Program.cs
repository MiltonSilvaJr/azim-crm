// PartnerManagement.Api — ponto de entrada do serviço partner-management.
// Módulo: BC-03 Partner Management (Supporting Subdomain).
// Design §3: Api -> Application, Infrastructure, Contracts.
// Onda 5: PartnersController, middleware de correlação/tenant/exceções, RBAC.

using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PartnerManagement.Api.Infrastructure;
using PartnerManagement.Api.Middleware;
using PartnerManagement.Application.Behaviors;
using PartnerManagement.Application.Ports;
using PartnerManagement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// =====================================================================
// MediatR — Application handlers e pipeline behaviors
// =====================================================================
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(PartnerManagement.Application.AssemblyReference).Assembly);

    // Ordem do pipeline (design §5.4):
    // 1. CorrelationLoggingBehavior
    // 2. TenantScopeBehavior
    // 3. ValidationBehavior
    // 4. AuthorizationBehavior
    // 5. TransactionBehavior
    cfg.AddOpenBehavior(typeof(CorrelationLoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(TenantScopeBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
});

// =====================================================================
// FluentValidation — validators da camada Application
// =====================================================================
builder.Services.AddValidatorsFromAssembly(
    typeof(PartnerManagement.Application.AssemblyReference).Assembly);

// =====================================================================
// Infrastructure — DbContext, repositórios, ports, adapters
// =====================================================================
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddPartnerManagementInfrastructure(builder.Configuration);
}

// =====================================================================
// IPermissionContext — lê claim permissions do JWT
// =====================================================================
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPermissionContext, JwtPermissionContext>();

// =====================================================================
// Autenticação JWT (produção) — substituída em testes pelo TestAuthHandler
// =====================================================================
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration["Auth:Authority"];
            options.Audience = builder.Configuration["Auth:Audience"];
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true
            };
        });
}

builder.Services.AddAuthorization();

// =====================================================================
// Controllers e OpenAPI
// =====================================================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// =====================================================================
// Pipeline HTTP
// =====================================================================

// 1. CorrelationId — primeiro (antes de tudo para rastreabilidade)
app.UseMiddleware<CorrelationIdMiddleware>();

// 2. Tratamento centralizado de exceções
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 3. Autenticação e autorização
app.UseAuthentication();
app.UseAuthorization();

// 4. Resolução de tenant (após auth — precisa do principal autenticado)
app.UseMiddleware<TenantResolutionMiddleware>();

// 5. Controllers
app.MapControllers();

// 6. Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

await app.RunAsync();

// Partial class para WebApplicationFactory nos testes de integração
public partial class Program { }

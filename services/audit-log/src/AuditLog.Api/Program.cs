using System.Text.Json.Serialization;
using AuditLog.Api.ErrorHandling;
using AuditLog.Api.Infrastructure;
using AuditLog.Application;
using AuditLog.Application.Abstractions;
using AuditLog.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------------------ JSON
// JsonStringEnumConverter + CamelCase: garante AuditAction serializado como "create"/"update"/"delete"
// UtcDateTimeOffsetConverter: garante createdAt como "...Z" (UTC ISO-8601)
// Pendência da Wave 1 resolvida aqui.
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter(
        System.Text.Json.JsonNamingPolicy.CamelCase));
    opts.SerializerOptions.Converters.Add(new AuditLog.Api.Infrastructure.UtcDateTimeOffsetConverter());
    opts.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(opts =>
{
    opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(
        System.Text.Json.JsonNamingPolicy.CamelCase));
    opts.JsonSerializerOptions.Converters.Add(new AuditLog.Api.Infrastructure.UtcDateTimeOffsetConverter());
    opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

// ------------------------------------------------------------------ Controllers + OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();

// ------------------------------------------------------------------ Autenticação JWT
var jwtAuthority = builder.Configuration["Jwt:Authority"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.Authority = jwtAuthority;
        opts.Audience = jwtAudience;
        // Em desenvolvimento, tolera authority ausente (sem Identity Platform local)
        opts.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        opts.Events = new JwtBearerEvents
        {
            OnChallenge = ctx =>
            {
                // Garante retorno de 401 antes de qualquer redirect
                ctx.HandleResponse();
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                ctx.Response.ContentType = "application/problem+json";
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ------------------------------------------------------------------ Contextos HTTP-bound
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddScoped<IUserContext, HttpUserContext>();
builder.Services.AddScoped<IBuScopeResolver, StubBuScopeResolver>();

// ------------------------------------------------------------------ Application + Infrastructure
builder.Services.AddAuditLogApplication();

var connectionString = builder.Configuration.GetConnectionString("AuditLog")
    ?? "Host=localhost;Port=5432;Database=audit_log;Username=postgres;Password=postgres";

builder.Services.AddAuditLogInfrastructure(connectionString);

// ------------------------------------------------------------------ Middleware de erros (Problem Details)
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AuditExceptionHandler>();

// ------------------------------------------------------------------ Build
var app = builder.Build();

// ------------------------------------------------------------------ Pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health check exposto em /health (design §11.5, RNF-006)
app.MapHealthChecks("/health");

app.Run();

// Necessário para WebApplicationFactory nos testes de integração
public partial class Program { }

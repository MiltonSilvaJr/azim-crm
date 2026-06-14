using System.Security.Claims;
using System.Text.Encodings.Web;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Application.Opportunities.Queries;
using OpportunityPipeline.Application.SavedFilters;
using OpportunityPipeline.Infrastructure.Scheduling;

namespace OpportunityPipeline.Api.Tests.Infrastructure;

/// <summary>
/// WebApplicationFactory configurada para testes de integração in-process.
/// Substitui: EF Core, autenticação JWT, portas externas, MediatR handlers.
/// Papel do usuário injetado via header X-Test-Role.
/// Serviço interno via header X-Service-Identity + X-Test-Is-Service.
/// Mapeia: design §13, TASK-20, TASK-21.
/// </summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // ---- Substituir esquema de autenticação padrão por handler de teste ---
            services.AddAuthentication("TestBearer")
                .AddScheme<AuthenticationSchemeOptions, TestBearerAuthHandler>(
                    "TestBearer", _ => { });

            services.Configure<AuthenticationOptions>(opts =>
            {
                opts.DefaultAuthenticateScheme = "TestBearer";
                opts.DefaultChallengeScheme = "TestBearer";
            });

            // ---- Substituir ITenantResolver para contexto controlável em teste ---
            services.RemoveAll<ITenantResolver>();
            services.AddScoped<ITenantResolver, TestTenantResolver>();

            // ---- Substituir IOpportunityQueryRepository por substituto ---
            services.RemoveAll<IOpportunityQueryRepository>();
            var queryRepo = Substitute.For<IOpportunityQueryRepository>();
            // Defaults: retorna listas vazias para evitar NullReferenceException nos testes de RBAC
            queryRepo.ListAsync(default, default, default!, default!, default)
                .ReturnsForAnyArgs(new PagedResult<OpportunitySummary>([], 0, 1, 20));
            queryRepo.GetKanbanAsync(default, default, default, default)
                .ReturnsForAnyArgs([]);
            queryRepo.GetTimelineAsync(default, default, default)
                .ReturnsForAnyArgs([]);
            queryRepo.GetCommissionsAsync(default, default, default)
                .ReturnsForAnyArgs([]);
            queryRepo.GetForecastAsync(default, default, default)
                .ReturnsForAnyArgs((0L, 0L, 0L));
            queryRepo.ListStaleAsync(default, default, default!, default)
                .ReturnsForAnyArgs(new PagedResult<OpportunitySummary>([], 0, 1, 50));
            services.AddSingleton(queryRepo);

            // ---- Substituir ISavedFilterRepository por substituto ---
            services.RemoveAll<ISavedFilterRepository>();
            var savedFilterRepo = Substitute.For<ISavedFilterRepository>();
            savedFilterRepo.ListByUserAsync(default, default, default)
                .ReturnsForAnyArgs([]);
            services.AddSingleton(savedFilterRepo);

            // ---- Substituir StaleScanEndpointHandler por wrapper de teste via factory delegate ---
            // StaleScanEndpointHandler é sealed — não pode ser substituído por NSubstitute.
            // Solução: registrar o StaleScanEndpointHandler via Func<> que retorna instância
            // com StagnationDetectionService usando suas dependências reais (também substituídas).
            services.RemoveAll<StaleScanEndpointHandler>();
            services.RemoveAll<Application.Opportunities.Services.StagnationDetectionService>();
            services.AddScoped<Application.Opportunities.Services.StagnationDetectionService>(sp =>
                new Application.Opportunities.Services.StagnationDetectionService(
                    Substitute.For<Domain.Opportunities.Repositories.IOpportunityRepository>(),
                    sp.GetRequiredService<IOpportunityQueryRepository>(),
                    Substitute.For<Domain.Opportunities.Ports.IActivityReadPort>(),
                    Substitute.For<IStaleDetectionRunRepository>(),
                    Substitute.For<Application.Behaviors.IUnitOfWork>(),
                    Substitute.For<Domain.Opportunities.Ports.IClock>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Application.Opportunities.Services.StagnationDetectionService>>()));
            services.AddScoped<StaleScanEndpointHandler>(sp =>
            {
                var clock = Substitute.For<Domain.Opportunities.Ports.IClock>();
                clock.UtcNow.Returns(DateTimeOffset.UtcNow);
                clock.Today.Returns(DateOnly.FromDateTime(DateTime.UtcNow));
                return new StaleScanEndpointHandler(
                    sp.GetRequiredService<Application.Opportunities.Services.StagnationDetectionService>(),
                    clock,
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<StaleScanEndpointHandler>>());
            });

            // ---- Remover serviços que dependem de EF Core/DB ---
            // Remove o OutboxPublisher (hosted service) para não tentar conectar ao DB
            var hostedServices = services
                .Where(s => s.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService))
                .ToList();
            foreach (var svc in hostedServices)
                services.Remove(svc);

            // ---- Substituir IMediator por stub para rotas que delegam ao MediatR ---
            services.RemoveAll<IMediator>();
            var mediator = Substitute.For<IMediator>();
            services.AddSingleton(mediator);

            // ---- Substituir TenantContext por implementação pré-inicializada para testes ---
            // Isso elimina a dependência do TenantBehavior sem alterar o código de produção.
            services.RemoveAll<Application.Common.TenantContext>();
            services.AddScoped<Application.Common.TenantContext>(_ =>
            {
                var ctx = new Application.Common.TenantContext();
                ctx.Initialize(
                    new Guid("11111111-1111-1111-1111-111111111111"),
                    new Guid("22222222-2222-2222-2222-222222222222"),
                    new Guid("33333333-3333-3333-3333-333333333333"));
                return ctx;
            });
        });

        builder.UseEnvironment("Testing");

        // Suprime log para não poluir output dos testes
        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
    }

    /// <summary>Cria client HTTP autenticado com o papel especificado.</summary>
    public HttpClient CreateClientWithRole(UserRole role)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", role.ToString());
        return client;
    }

    /// <summary>Cria client HTTP com identidade de serviço para /internal/*.</summary>
    public HttpClient CreateClientWithServiceIdentity(string serviceIdentity = "cloud-scheduler")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(
            Auth.ServiceIdentityAuthHandler.ServiceIdentityHeader,
            serviceIdentity);
        client.DefaultRequestHeaders.Add("X-Test-Is-Service", "true");
        return client;
    }

    /// <summary>Cria client HTTP sem autenticação (para testar 401).</summary>
    public HttpClient CreateUnauthenticatedClient()
    {
        var options = new WebApplicationFactoryClientOptions { AllowAutoRedirect = false };
        return CreateClient(options);
    }
}

/// <summary>
/// Handler de autenticação de teste.
/// Extrai role do header X-Test-Role.
/// Serviço interno detectado via X-Test-Is-Service + X-Service-Identity.
/// </summary>
public sealed class TestBearerAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Serviço interno — autentica com scheme ServiceIdentity para satisfazer a política
        if (Request.Headers.TryGetValue("X-Test-Is-Service", out _) &&
            Request.Headers.TryGetValue(Auth.ServiceIdentityAuthHandler.ServiceIdentityHeader, out var svcHeader))
        {
            var svcIdentity = svcHeader.ToString();
            var svcClaims = new[]
            {
                new Claim(ClaimTypes.Name, svcIdentity),
                new Claim("service_identity", svcIdentity),
                new Claim("is_service", "true")
            };
            var svcPrincipal = new ClaimsPrincipal(
                new ClaimsIdentity(svcClaims, Auth.ServiceIdentityAuthHandler.SchemeName));
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(svcPrincipal, Auth.ServiceIdentityAuthHandler.SchemeName)));
        }

        // Usuário autenticado via JWT simulado
        var tenantId = "11111111-1111-1111-1111-111111111111";
        var buId = "22222222-2222-2222-2222-222222222222";
        var actorId = "33333333-3333-3333-3333-333333333333";
        var role = Request.Headers.TryGetValue("X-Test-Role", out var roleHeader)
            ? roleHeader.ToString()
            : "Viewer";

        var claims = new[]
        {
            new Claim("tenant_id", tenantId),
            new Claim("bu_id", buId),
            new Claim(ClaimTypes.NameIdentifier, actorId),
            new Claim("role", role),
            new Claim(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, "TestBearer");
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(principal, "TestBearer")));
    }
}

/// <summary>
/// Resolver de tenant para testes — lê IDs das claims do usuário autenticado.
/// </summary>
public sealed class TestTenantResolver(IHttpContextAccessor httpContextAccessor) : ITenantResolver
{
    public Task<(Guid TenantId, Guid BuId, Guid ActorId, UserRole Role)> ResolveAsync(
        CancellationToken cancellationToken = default)
    {
        var ctx = httpContextAccessor.HttpContext;
        if (ctx?.User.Identity?.IsAuthenticated != true)
            return Task.FromResult((Guid.Empty, Guid.Empty, Guid.Empty, UserRole.Viewer));

        var user = ctx.User;
        Guid.TryParse(user.FindFirstValue("tenant_id") ?? "", out var tenantId);
        Guid.TryParse(user.FindFirstValue("bu_id") ?? "", out var buId);
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", out var actorId);
        Enum.TryParse<UserRole>(
            user.FindFirstValue("role") ?? "",
            ignoreCase: true,
            out var role);
        return Task.FromResult((tenantId, buId, actorId, role));
    }
}

/// <summary>
/// IStartupFilter que insere um middleware no início do pipeline para inicializar
/// o TenantContext antes que os controllers tentem acessá-lo.
/// Apenas para ambiente de testes (sem MediatR pipeline TenantBehavior).
/// </summary>
public sealed class TenantContextInitializerFilter : Microsoft.AspNetCore.Hosting.IStartupFilter
{
    public Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> Configure(
        Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use((RequestDelegate next2) => async httpContext =>
            {
                // Executa autenticação antes de acessar User.Identity
                await httpContext.AuthenticateAsync().ConfigureAwait(false);

                var tenantCtx = httpContext.RequestServices
                    .GetRequiredService<Application.Common.TenantContext>();

                if (!tenantCtx.IsInitialized && httpContext.User.Identity?.IsAuthenticated == true)
                {
                    const string defaultTenantId = "11111111-1111-1111-1111-111111111111";
                    const string defaultBuId = "22222222-2222-2222-2222-222222222222";
                    const string defaultActorId = "33333333-3333-3333-3333-333333333333";

                    Guid.TryParse(
                        httpContext.User.FindFirstValue("tenant_id") ?? defaultTenantId,
                        out var tenantId);
                    Guid.TryParse(
                        httpContext.User.FindFirstValue("bu_id") ?? defaultBuId,
                        out var buId);
                    Guid.TryParse(
                        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? defaultActorId,
                        out var actorId);

                    if (tenantId != Guid.Empty && buId != Guid.Empty && actorId != Guid.Empty)
                        tenantCtx.Initialize(tenantId, buId, actorId);
                }

                await next2(httpContext).ConfigureAwait(false);
            });

            next(app);
        };
    }
}

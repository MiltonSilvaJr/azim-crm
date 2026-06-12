using AuditLog.Application.Abstractions;
using AuditLog.Application.Queries;
using AuditLog.Application.Results;
using AuditLog.Domain.Aggregates;
using AuditLog.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.Core;

namespace AuditLog.Api.Tests.Helpers;

/// <summary>
/// Factory de WebApplicationFactory para testes de API do AuditLog.
/// Substitui dependências de infraestrutura por doubles de teste (NSubstitute)
/// e registra o esquema de autenticação de teste.
/// </summary>
public sealed class AuditApiFactory : WebApplicationFactory<Program>
{
    // Defaults públicos para configuração dos doubles
    public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DefaultEntityId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid DefaultUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    /// <summary>ISender mockado para interceptar queries MediatR.</summary>
    public ISender SenderMock { get; } = Substitute.For<ISender>();

    /// <summary>IAuditMetrics mockada para testes de observabilidade.</summary>
    public IAuditMetrics MetricsMock { get; } = Substitute.For<IAuditMetrics>();

    /// <summary>IBuScopeResolver mockado para testes de escopo de BU.</summary>
    public IBuScopeResolver BuScopeResolverMock { get; } = Substitute.For<IBuScopeResolver>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove TODOS os registros de ISender (MediatR pode registrar mais de um) e substitui pelo mock
            var senderDescriptors = services.Where(d => d.ServiceType == typeof(ISender)).ToList();
            foreach (var d in senderDescriptors)
                services.Remove(d);
            services.AddSingleton(SenderMock);

            // Remove IMediator também pois pode ser resolvido em vez de ISender
            var mediatorDescriptors = services.Where(d =>
                d.ServiceType.FullName?.Contains("IMediator") == true ||
                d.ServiceType.FullName?.Contains("IPublisher") == true).ToList();
            foreach (var d in mediatorDescriptors)
                services.Remove(d);

            // Substitui IAuditMetrics pelo mock
            var metricsDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAuditMetrics));
            if (metricsDescriptor is not null)
                services.Remove(metricsDescriptor);
            services.AddSingleton(MetricsMock);

            // Substitui IBuScopeResolver pelo mock
            var buDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IBuScopeResolver));
            if (buDescriptor is not null)
                services.Remove(buDescriptor);
            services.AddSingleton(BuScopeResolverMock);

            // Substitui ITenantContext pelo TenantContextFromHeader (deriva do header de teste)
            var tenantDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ITenantContext));
            if (tenantDescriptor is not null)
                services.Remove(tenantDescriptor);
            services.AddScoped<ITenantContext, TenantContextFromHttpContext>();

            // Substitui IUserContext pelo UserContextFromHeader
            var userDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IUserContext));
            if (userDescriptor is not null)
                services.Remove(userDescriptor);
            services.AddScoped<IUserContext, UserContextFromHttpContext>();

            // Adiciona esquema de autenticação de teste
            services
                .AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<TestAuthHandlerOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });
        });
    }

    /// <summary>
    /// Cria um HttpClient configurado com o papel e tenant informados.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string role, Guid? tenantId = null, Guid? userId = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.TenantIdHeader,
            (tenantId ?? DefaultTenantId).ToString());
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.UserIdHeader,
            (userId ?? DefaultUserId).ToString());
        return client;
    }

    /// <summary>Cria HttpClient sem credenciais (para testar 401).</summary>
    public HttpClient CreateUnauthenticatedClient() => CreateClient();

    /// <summary>
    /// Configura o ISender mock para retornar um PagedResult vazio para qualquer query
    /// de <see cref="ListAuditLogsQuery"/> e <see cref="GetEntityAuditHistoryQuery"/>.
    /// Útil para testes que focam em autenticação e autorização.
    /// </summary>
    public void SetupEmptyListResponse()
    {
        var empty = new PagedResult<AuditLogAggregate>(
            Array.Empty<AuditLogAggregate>(), 1, 50, 0);

        SenderMock
            .Send(Arg.Any<ListAuditLogsQuery>(), Arg.Any<CancellationToken>())
            .Returns(empty);

        SenderMock
            .Send(Arg.Any<GetEntityAuditHistoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(empty);
    }

    /// <summary>
    /// Configura o ISender mock para retornar itens de auditoria na resposta.
    /// </summary>
    public void SetupListResponse(IReadOnlyList<AuditLogAggregate> items, int total = -1)
    {
        var actualTotal = total < 0 ? items.Count : total;
        var result = new PagedResult<AuditLogAggregate>(items, 1, 50, actualTotal);

        SenderMock
            .Send(Arg.Any<ListAuditLogsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        SenderMock
            .Send(Arg.Any<GetEntityAuditHistoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);
    }
}

/// <summary>
/// Implementação de <see cref="ITenantContext"/> que lê o tenant_id do cabeçalho HTTP de teste.
/// </summary>
internal sealed class TenantContextFromHttpContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContextFromHttpContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? TenantId
    {
        get
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx is null) return null;

            if (ctx.Request.Headers.TryGetValue(TestAuthHandler.TenantIdHeader, out var val)
                && Guid.TryParse(val.ToString(), out var id))
            {
                return id;
            }

            // Tenta derivar do claim do token
            var tenantClaim = ctx.User?.FindFirst("tenant_id")?.Value;
            if (tenantClaim is not null && Guid.TryParse(tenantClaim, out var claimId))
                return claimId;

            return null;
        }
    }
}

/// <summary>
/// Implementação de <see cref="IUserContext"/> que lê o papel do cabeçalho HTTP de teste.
/// </summary>
internal sealed class UserContextFromHttpContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserContextFromHttpContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? Role
    {
        get
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx is null) return null;

            if (ctx.Request.Headers.TryGetValue(TestAuthHandler.RoleHeader, out var val))
                return val.ToString();

            return ctx.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        }
    }
}

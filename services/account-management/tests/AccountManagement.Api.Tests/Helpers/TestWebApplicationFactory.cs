using AccountManagement.Application.Behaviors;
using AccountManagement.Application.Ports;
using AccountManagement.Domain.Accounts.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace AccountManagement.Api.Tests.Helpers;

/// <summary>
/// Opções de autenticação de teste — permite configurar o papel padrão do usuário simulado.
/// O papel por request é injetado via header <c>X-Test-Role</c>.
/// Mapeia: design §13, TASK-13..15.
/// </summary>
public sealed class TestAuthOptions : AuthenticationSchemeOptions
{
    /// <summary>Papel padrão injetado nos claims quando o header X-Test-Role não está presente.</summary>
    public string Role { get; set; } = "Viewer";
}

/// <summary>
/// Factory de WebApplication para testes de API do módulo account-management.
///
/// Substitui o pipeline de autenticação por um handler de teste que injeta claims
/// controladas por header <c>X-Test-Role</c> (RBAC por papel — Req 9, design §10).
///
/// O <see cref="ISender"/> é substituído por mock NSubstitute para isolar os testes
/// de API da camada Application/Infrastructure (testes de controller em isolamento).
///
/// Interfaces de infraestrutura (<see cref="IAccountRepository"/>, <see cref="IUnitOfWork"/>,
/// <see cref="IClock"/>, <see cref="IEventPublisher"/>, <see cref="IAuditPublisher"/>,
/// <see cref="IOpportunityReadPort"/>, <see cref="IActivityReadPort"/>) são registradas
/// como mocks NSubstitute para satisfazer a validação de DI no startup — sem connection string
/// no contexto de testes.
///
/// Mapeia: design §13 (testes de API), TASK-13..TASK-15.
/// </summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>Tenant padrão usado nos testes.</summary>
    public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>Usuário padrão usado nos testes.</summary>
    public static readonly Guid DefaultUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>Sender MediatR mockado — configurável nos testes para retornar respostas controladas.</summary>
    public ISender Sender { get; } = Substitute.For<ISender>();

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureTestServices(services =>
        {
            // =====================================================================
            // Substitui autenticação real por handler de teste
            // Permite injetar papel via header X-Test-Role por request
            // =====================================================================
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<TestAuthOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

            // =====================================================================
            // Substitui ISender (MediatR) pelo mock configurável
            // Todos os testes de controller controlam o comportamento via Sender mock
            // =====================================================================
            services.RemoveAll<ISender>();
            services.AddSingleton(Sender);

            // =====================================================================
            // Mocks de infraestrutura para satisfazer a validação de DI no startup.
            // Como ISender é mockado, nenhum handler chama esses serviços.
            // São necessários apenas para que o container de DI seja construído sem erros.
            // =====================================================================
            services.AddSingleton(Substitute.For<IAccountRepository>());
            services.AddSingleton(Substitute.For<IUnitOfWork>());
            services.AddSingleton(Substitute.For<IClock>());
            services.AddSingleton(Substitute.For<IEventPublisher>());
            services.AddSingleton(Substitute.For<IAuditPublisher>());
            services.AddSingleton(Substitute.For<IOpportunityReadPort>());
            services.AddSingleton(Substitute.For<IActivityReadPort>());
        });

        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
    }

    /// <summary>
    /// Cria um <see cref="HttpClient"/> autenticado com o papel especificado.
    /// O papel é transmitido via header <c>X-Test-Role</c> em cada request do cliente.
    /// </summary>
    /// <param name="role">Papel do usuário nos claims (ex.: "Viewer", "Vendedor", "TenantAdmin").</param>
    public HttpClient CreateClientWithRole(string role)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        return client;
    }

    /// <summary>
    /// Cria um <see cref="HttpClient"/> autenticado com o papel especificado e BUs autorizadas.
    /// Usado nos testes PBT-05 para simular escopo de BUs do usuário.
    /// </summary>
    /// <param name="role">Papel do usuário.</param>
    /// <param name="buIds">BUs autorizadas, separadas por vírgula.</param>
    public HttpClient CreateClientWithRoleAndBuIds(string role, string buIds)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        client.DefaultRequestHeaders.Add("X-Test-BuIds", buIds);
        return client;
    }
}

// =============================================================================
// Handler de autenticação de teste
// =============================================================================

/// <summary>
/// Handler de autenticação que injeta claims de teste sem validação JWT real.
///
/// Lê o papel do header <c>X-Test-Role</c> (quando presente) para permitir variar
/// o papel por request sem recriar a factory.
/// Lê BUs autorizadas do header <c>X-Test-BuIds</c> (quando presente).
///
/// Permite testar RBAC por papel de forma controlada (design §10, Req 9).
/// Não expõe PII nos claims de teste.
/// </summary>
internal sealed class TestAuthHandler : AuthenticationHandler<TestAuthOptions>
{
    public const string SchemeName = "Test";

    public TestAuthHandler(
        IOptionsMonitor<TestAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Papel lido do header por request (permite variar sem recriar factory)
        var role = Request.Headers.TryGetValue("X-Test-Role", out var roleHeader)
            && !string.IsNullOrWhiteSpace(roleHeader)
            ? roleHeader.ToString()
            : Options.Role;

        // BUs autorizadas lidas do header (para testes PBT-05)
        var buIds = Request.Headers.TryGetValue("X-Test-BuIds", out var buIdsHeader)
            && !string.IsNullOrWhiteSpace(buIdsHeader)
            ? buIdsHeader.ToString()
            : string.Empty;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, TestWebApplicationFactory.DefaultUserId.ToString()),
            new("tenant_id", TestWebApplicationFactory.DefaultTenantId.ToString()),
            new("role", role),
        };

        if (!string.IsNullOrEmpty(buIds))
            claims.Add(new Claim("bu_ids", buIds));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

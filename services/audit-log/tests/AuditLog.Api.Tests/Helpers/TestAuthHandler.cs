using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuditLog.Api.Tests.Helpers;

/// <summary>
/// Handler de autenticação para testes de integração com WebApplicationFactory.
/// Emite um ClaimsPrincipal cujo papel e tenant são configuráveis por request
/// via cabeçalho customizado, permitindo simular diferentes perfis RBAC.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<TestAuthHandlerOptions>
{
    public const string SchemeName = "TestAuth";

    /// <summary>Cabeçalho HTTP que define o papel do usuário no teste.</summary>
    public const string RoleHeader = "X-Test-Role";

    /// <summary>Cabeçalho HTTP que define o tenant_id do usuário no teste.</summary>
    public const string TenantIdHeader = "X-Test-TenantId";

    /// <summary>Cabeçalho HTTP que define o user_id do usuário no teste.</summary>
    public const string UserIdHeader = "X-Test-UserId";

    public TestAuthHandler(
        IOptionsMonitor<TestAuthHandlerOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RoleHeader, out var roleValues))
        {
            // Sem cabeçalho de papel → sem autenticação (401)
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var role = roleValues.ToString();
        var tenantId = Request.Headers.TryGetValue(TenantIdHeader, out var tenantValues)
            ? tenantValues.ToString()
            : Guid.NewGuid().ToString();
        var userId = Request.Headers.TryGetValue(UserIdHeader, out var userValues)
            ? userValues.ToString()
            : Guid.NewGuid().ToString();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, role),
            new("tenant_id", tenantId)
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>Opções para o <see cref="TestAuthHandler"/>.</summary>
public sealed class TestAuthHandlerOptions : AuthenticationSchemeOptions { }

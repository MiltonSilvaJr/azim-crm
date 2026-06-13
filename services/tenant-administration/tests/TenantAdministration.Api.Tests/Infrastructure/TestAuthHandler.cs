using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TenantAdministration.Api.Tests.Infrastructure;

/// <summary>
/// Handler de autenticação de teste que usa o header <c>X-Test-Role</c>
/// para simular diferentes papéis sem necessidade de JWT real.
/// Valores aceitos: <c>PlatformOperator</c>, <c>TenantAdmin</c>, <c>Viewer</c>.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers["X-Test-Role"].FirstOrDefault() ?? string.Empty;
        var tenantId = Request.Headers["X-Test-TenantId"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(role))
            return Task.FromResult(AuthenticateResult.Fail("Header X-Test-Role ausente."));

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "test-user"),
            new(ClaimTypes.NameIdentifier, "test-user-id"),
            new("role", role)
        };

        if (!string.IsNullOrWhiteSpace(tenantId))
            claims.Add(new Claim("tenant_id", tenantId));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

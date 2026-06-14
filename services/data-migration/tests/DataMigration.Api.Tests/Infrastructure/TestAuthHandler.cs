using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataMigration.Api.Tests.Infrastructure;

/// <summary>
/// Handler de autenticação de teste para WebApplicationFactory.
/// Injeta claims de tenant e papel via header HTTP.
///
/// Headers esperados:
///   - <c>X-Test-Role</c>: papel do usuário (PlatformOperator | TenantAdmin).
///   - <c>X-Test-TenantId</c>: UUID do tenant.
///   - <c>X-Test-UserId</c>: UUID do usuário.
///
/// Rastreia: design §10, TASK-21.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Role", out var roleValues))
        {
            return Task.FromResult(AuthenticateResult.Fail("Header X-Test-Role ausente."));
        }

        var tenantId = Request.Headers.TryGetValue("X-Test-TenantId", out var tenantValues)
            ? tenantValues.FirstOrDefault()
            : Guid.NewGuid().ToString();

        var userId = Request.Headers.TryGetValue("X-Test-UserId", out var userValues)
            ? userValues.FirstOrDefault()
            : Guid.NewGuid().ToString();

        var role = roleValues.FirstOrDefault() ?? string.Empty;

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "test-user"),
            new Claim(ClaimTypes.Role, role),
            new Claim("tenant_id", tenantId ?? Guid.NewGuid().ToString()),
            new Claim("user_id", userId ?? Guid.NewGuid().ToString()),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

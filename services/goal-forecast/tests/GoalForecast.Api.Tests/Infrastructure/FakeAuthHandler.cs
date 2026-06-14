using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoalForecast.Api.Tests.Infrastructure;

/// <summary>
/// Handler de autenticação fake para testes de API com WebApplicationFactory.
/// Injeta claims de papel (role), tenant, BU e usuário via header HTTP.
///
/// Uso: incluir header "X-Test-Claims" com valor em formato CSV de pares
/// "key=value", ex.: "role=TenantAdmin,tenantId=&lt;guid&gt;,userId=&lt;guid&gt;".
///
/// Mapeia: TASK-22..25 (testes RBAC), design §10.
/// </summary>
public sealed class FakeAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestAuth";

    public FakeAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers["X-Test-Claims"].FirstOrDefault();

        if (string.IsNullOrEmpty(header))
            return Task.FromResult(AuthenticateResult.Fail("Missing X-Test-Claims header"));

        var claims = ParseClaims(header);
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static IEnumerable<Claim> ParseClaims(string header)
    {
        // Formato: "role=TenantAdmin,tenantId=<guid>,userId=<guid>,buId=<guid>"
        return header.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .Select(parts => new Claim(parts[0].Trim(), parts[1].Trim()));
    }
}

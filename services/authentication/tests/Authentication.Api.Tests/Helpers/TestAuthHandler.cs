using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authentication.Api.Tests.Helpers;

/// <summary>
/// Handler de autenticação de teste que injeta um AuthContext fixo sem acionar Firebase.
///
/// Usado pela WebApplicationFactory para substituir autenticação real nos testes de API.
/// Mapeia: TASK-15..TASK-20, requisito de testes sem Firebase real.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestAuth";

    private static ClaimsPrincipal? _currentPrincipal;

    public static void SetCurrentPrincipal(ClaimsPrincipal? principal)
        => _currentPrincipal = principal;

    public static void ClearPrincipal()
        => _currentPrincipal = null;

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (_currentPrincipal is null)
            return Task.FromResult(AuthenticateResult.NoResult());

        var ticket = new AuthenticationTicket(_currentPrincipal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

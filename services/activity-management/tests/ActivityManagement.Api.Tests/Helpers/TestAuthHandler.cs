namespace ActivityManagement.Api.Tests.Helpers;

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Handler de autenticação de teste que substitui o JWT Bearer nos testes de API.
/// Permite configurar claims por papel/tenant sem necessidade de token JWT real.
/// Mapeia: TASK-18, TASK-21.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<TestAuthOptions>
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
        var claims = Options.Claims;
        if (claims is null || !claims.Any())
            return Task.FromResult(AuthenticateResult.Fail("Sem claims configuradas"));

        var identity  = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket    = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>
/// Opções do handler de autenticação de teste.
/// </summary>
public sealed class TestAuthOptions : AuthenticationSchemeOptions
{
    /// <summary>Claims injetadas no usuário autenticado de teste.</summary>
    public IEnumerable<Claim>? Claims { get; set; }
}

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PartnerManagement.Api.Tests.Helpers;

/// <summary>
/// Handler de autenticação de teste que simula JWT com claims configuráveis por papel.
/// Permite que testes de integração com WebApplicationFactory controlem o RBAC
/// sem precisar de token JWT real.
/// Mapeia: TASK-23, TASK-25, design §10.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestAuth";

    /// <summary>Header HTTP para injetar claims no formato "claim1=value1;claim2=value2".</summary>
    public const string TestClaimsHeader = "X-Test-Claims";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Se não há header de claims de teste, retorna falha (não autenticado)
        if (!Request.Headers.TryGetValue(TestClaimsHeader, out var claimsHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>();

        // Parsear claims do header: "key=value;key2=value2"
        // Múltiplas entradas com a mesma chave são permitidas (ex.: permissions=X;permissions=Y)
        foreach (string? headerValue in claimsHeader)
        {
            if (headerValue is null)
            {
                continue;
            }
            foreach (string pair in headerValue.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                int eqIdx = pair.IndexOf('=');
                if (eqIdx > 0)
                {
                    string key = pair[..eqIdx].Trim();
                    string val = pair[(eqIdx + 1)..].Trim();
                    claims.Add(new Claim(key, val));
                }
            }
        }

        // Garantir sub (identity) se não fornecido
        if (!claims.Any(c => c.Type == ClaimTypes.NameIdentifier))
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

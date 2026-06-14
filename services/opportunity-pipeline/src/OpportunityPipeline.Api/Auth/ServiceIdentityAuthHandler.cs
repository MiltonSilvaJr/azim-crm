using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace OpportunityPipeline.Api.Auth;

/// <summary>
/// Handler de autenticação para endpoints internos (/internal/*).
/// Verifica identidade de serviço via header X-Service-Identity.
/// Em produção: substituir por validação de certificado mTLS ou Google Cloud Identity Token.
/// Mapeia: design §8 (endpoints internos), §10 (ServiceIdentity), rule mtls-internal-services, TASK-21.
/// </summary>
public sealed class ServiceIdentityAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>Nome do esquema de autenticação de serviço interno.</summary>
    public const string SchemeName = "ServiceIdentity";

    /// <summary>Header esperado com a identidade do serviço (ex.: "cloud-scheduler" ou "internal-service").</summary>
    public const string ServiceIdentityHeader = "X-Service-Identity";

    /// <summary>Valores aceitos como identidade de serviço válida.</summary>
    private static readonly HashSet<string> ValidServiceIdentities =
        new(StringComparer.OrdinalIgnoreCase) { "cloud-scheduler", "internal-service", "stale-scanner", "goal-forecast" };

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ServiceIdentityHeader, out var serviceIdentityValues))
        {
            return Task.FromResult(AuthenticateResult.Fail("Header X-Service-Identity ausente."));
        }

        var serviceIdentity = serviceIdentityValues.ToString();

        if (!ValidServiceIdentities.Contains(serviceIdentity))
        {
            return Task.FromResult(AuthenticateResult.Fail(
                $"Identidade de serviço inválida: '{serviceIdentity}'."));
        }

        // Identidade válida: cria claims principal
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, serviceIdentity),
            new Claim("service_identity", serviceIdentity),
            new Claim("is_service", "true")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

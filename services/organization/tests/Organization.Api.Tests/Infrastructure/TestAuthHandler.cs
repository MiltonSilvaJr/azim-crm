using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Organization.Api.Tests.Infrastructure;

/// <summary>
/// Handler de autenticação de teste para substituir JWT em testes de integração.
/// Constrói um ClaimsPrincipal a partir dos claims configurados via <see cref="TestAuthOptions"/>.
/// Permite simular diferentes papéis e tenants sem emitir JWTs reais.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly TestAuthOptions _testOptions;

    /// <summary>Inicializa o handler.</summary>
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TestAuthOptions testOptions)
        : base(options, logger, encoder)
    {
        _testOptions = testOptions;
    }

    /// <inheritdoc/>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (_testOptions.CurrentClaims is null)
        {
            // NoResult permite que endpoints [AllowAnonymous] funcionem sem 401.
            // Fail causaria challenge e retornaria 401 mesmo em endpoints públicos.
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(_testOptions.CurrentClaims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>
/// Opções de autenticação de teste — permite configurar claims por test case.
/// Registrado como singleton para que o factory e os testes compartilhem a mesma instância.
/// </summary>
public sealed class TestAuthOptions
{
    /// <summary>Claims do usuário autenticado no próximo request.</summary>
    public IEnumerable<Claim>? CurrentClaims { get; set; }
}

/// <summary>
/// Constante de scheme de autenticação de teste.
/// </summary>
public static class TestAuthScheme
{
    /// <summary>Nome do scheme.</summary>
    public const string Name = "Test";
}

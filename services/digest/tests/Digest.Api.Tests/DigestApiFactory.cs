using System.Security.Claims;
using System.Text.Encodings.Web;
using Digest.Application.Models;
using Digest.Application.Ports;
using Digest.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Digest.Api.Tests;

/// <summary>
/// Factory de <see cref="WebApplicationFactory{TEntryPoint}"/> para testes de API do digest (TASK-21/22).
/// Substitui autenticação JWT Bearer real por handler de teste controlado por header customizado.
/// Substitui dependências de infraestrutura (MediatR, portas) por stubs/mocks.
/// </summary>
public sealed class DigestApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Header customizado de teste que simula a presença de token OIDC.</summary>
    public const string TestAuthHeader = "X-Test-Auth";

    /// <summary>Valor do header que representa identidade autorizada (SA do Cloud Scheduler).</summary>
    public const string AuthorizedTestToken = "authorized-scheduler-sa";

    /// <summary>Valor do header que representa identidade autenticada mas não autorizada.</summary>
    public const string UnauthorizedTestToken = "unauthorized-other-sa";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // ---------------------------------------------------------------
            // Substitui autenticação JWT Bearer pelo handler de teste
            // ---------------------------------------------------------------
            // Remove o esquema JWT Bearer existente
            services.Configure<AuthenticationOptions>(opts =>
            {
                opts.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                opts.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            });

            // Remove o handler JWT Bearer e adiciona o de teste
            services.RemoveAll<IConfigureOptions<JwtBearerOptions>>();
            services.RemoveAll<IPostConfigureOptions<JwtBearerOptions>>();

            services.AddAuthentication(opts =>
            {
                opts.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                opts.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            // ---------------------------------------------------------------
            // Stub do IMediator — SelectEligibleTenantsQuery retorna lista vazia
            // ---------------------------------------------------------------
            var mediator = Substitute.For<IMediator>();

            mediator.Send(
                Arg.Any<SelectEligibleTenantsQuery>(),
                Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>()));

            services.RemoveAll<IMediator>();
            services.AddSingleton(mediator);

            // ---------------------------------------------------------------
            // Remove portas de leitura reais (exigem DB/HTTP interno)
            // ---------------------------------------------------------------
            services.RemoveAll<IUserDirectoryPort>();
            var userDirectoryPort = Substitute.For<IUserDirectoryPort>();
            userDirectoryPort.GetActiveTenantInfosAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<TenantInfo>>(Array.Empty<TenantInfo>()));
            services.AddSingleton(userDirectoryPort);

            // ---------------------------------------------------------------
            // Remove IPubSubFanout real — substitui por stub que não faz nada
            // ---------------------------------------------------------------
            services.RemoveAll<Digest.Api.Infrastructure.IPubSubFanout>();
            var fanoutStub = Substitute.For<Digest.Api.Infrastructure.IPubSubFanout>();
            fanoutStub.PublishFanoutMessagesAsync(
                Arg.Any<IReadOnlyList<Guid>>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            services.AddSingleton(fanoutStub);
        });

        builder.UseEnvironment("Testing");
    }
}

/// <summary>
/// Handler de autenticação customizado para testes de API (substitui JWT Bearer real).
/// Lógica de autenticação:
/// - Sem header <see cref="DigestApiFactory.TestAuthHeader"/> → falha de autenticação (401).
/// - Header com valor <see cref="DigestApiFactory.AuthorizedTestToken"/> → SA autorizada (202/OK).
/// - Header com valor <see cref="DigestApiFactory.UnauthorizedTestToken"/> → SA não autorizada (403).
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestScheme";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Sem header → não autenticado (desafia com 401)
        if (!Request.Headers.TryGetValue(DigestApiFactory.TestAuthHeader, out var headerValues))
            return Task.FromResult(AuthenticateResult.NoResult());

        var headerValue = headerValues.ToString();

        // Valor do header não reconhecido → não autenticado
        if (headerValue != DigestApiFactory.AuthorizedTestToken
            && headerValue != DigestApiFactory.UnauthorizedTestToken)
        {
            return Task.FromResult(AuthenticateResult.Fail("Token de teste inválido."));
        }

        // Cria claims conforme o valor do header
        var email = headerValue == DigestApiFactory.AuthorizedTestToken
            ? "cloud-scheduler@azim-prod.iam.gserviceaccount.com"   // SA autorizada
            : "other-service@azim-prod.iam.gserviceaccount.com";    // SA não autorizada

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, email),
            new Claim("email", email),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

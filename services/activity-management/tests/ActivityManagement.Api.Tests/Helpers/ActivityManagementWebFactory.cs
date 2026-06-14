namespace ActivityManagement.Api.Tests.Helpers;

using System.Security.Claims;
using ActivityManagement.Application.Activities.Commands;
using ActivityManagement.Application.Ports;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

/// <summary>
/// WebApplicationFactory configurada para testes de API com autenticação de teste e
/// mocks de MediatR/portas — sem banco de dados real (testes unitários de contrato).
/// Para testes de integração cross-tenant com banco real, use subclasse separada.
/// Mapeia: TASK-18, TASK-21.
/// </summary>
public sealed class ActivityManagementWebFactory : WebApplicationFactory<Program>
{
    private IEnumerable<Claim> _claims = TestJwtHelper.SellerClaims(Guid.NewGuid());
    private readonly Dictionary<Type, object> _substitutes = new();

    /// <summary>
    /// Configura as claims do usuário autenticado para o cliente criado por esta factory.
    /// </summary>
    public ActivityManagementWebFactory WithClaims(IEnumerable<Claim> claims)
    {
        _claims = claims;
        return this;
    }

    /// <summary>
    /// Substitui um serviço registrado no DI por um mock NSubstitute.
    /// </summary>
    public ActivityManagementWebFactory WithSubstitute<T>(T substitute) where T : class
    {
        _substitutes[typeof(T)] = substitute;
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Substitui autenticação JWT por handler de teste
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<TestAuthOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    opts => opts.Claims = _claims);

            // Registra substitutos configurados
            foreach (var (type, substitute) in _substitutes)
            {
                services.AddSingleton(type, substitute);
            }

            // Substituto padrão de MediatR para isolar controllers de handlers reais
            if (!_substitutes.ContainsKey(typeof(IMediator)))
            {
                services.AddSingleton(Substitute.For<IMediator>());
            }
        });
    }

    /// <summary>
    /// Cria um cliente HTTP com as claims configuradas.
    /// </summary>
    public HttpClient CreateAuthenticatedClient()
    {
        var factory = new ActivityManagementWebFactory();
        factory._claims = _claims;
        foreach (var (type, sub) in _substitutes)
            factory._substitutes[type] = sub;

        return factory.CreateClient();
    }
}

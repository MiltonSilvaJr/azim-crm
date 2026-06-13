using Authentication.Application.Ports;
using Authentication.Application.Ports.Results;
using Authentication.Application.Services;
using Authentication.Domain.ValueObjects;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Authentication.Api.Tests.Helpers;

/// <summary>
/// WebApplicationFactory configurada para testes de API do módulo authentication.
///
/// Substitui dependências externas (Firebase, Redis, PostgreSQL) por mocks (NSubstitute)
/// para que os testes sejam rápidos, determinísticos e sem efeito colateral externo.
///
/// Mapeia: TASK-15..TASK-20, design.md § 13.
/// </summary>
public sealed class AuthenticationApiFactory : WebApplicationFactory<Program>
{
    // Mocks expostos para configuração por teste
    public ITenantDirectory TenantDirectory { get; } = Substitute.For<ITenantDirectory>();
    public IIdentityProvider IdentityProvider { get; } = Substitute.For<IIdentityProvider>();
    public IUserDirectory UserDirectory { get; } = Substitute.For<IUserDirectory>();
    public IRateLimiter RateLimiter { get; } = Substitute.For<IRateLimiter>();
    public IEmailSender EmailSender { get; } = Substitute.For<IEmailSender>();
    public IAuditEventEmitter AuditEmitter { get; } = Substitute.For<IAuditEventEmitter>();

    // TenantId padrão para testes
    public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly string DefaultTenantSlug = "acme";
    public static readonly string DefaultFirebaseTenant = "firebase-tenant-acme";

    // UserId padrão para testes
    public static readonly Guid DefaultUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Substituir implementações reais por mocks
            services.RemoveAll<ITenantDirectory>();
            services.RemoveAll<IIdentityProvider>();
            services.RemoveAll<IUserDirectory>();
            services.RemoveAll<IRateLimiter>();
            services.RemoveAll<IEmailSender>();
            services.RemoveAll<IAuditEventEmitter>();

            // Registrar mocks
            services.AddSingleton(TenantDirectory);
            services.AddSingleton(IdentityProvider);
            services.AddSingleton(UserDirectory);
            services.AddSingleton(RateLimiter);
            services.AddSingleton(EmailSender);
            services.AddSingleton(AuditEmitter);

            // Cache em memória para AuthContextComposer
            services.AddMemoryCache();

            // Registrar serviços de aplicação que dependem dos mocks
            services.AddScoped<SessionTokenValidator>();
            services.AddScoped<AuthContextComposer>();
            services.AddScoped<SessionRevocationService>();
            services.AddScoped<InviteActivationService>();
            services.AddScoped<PasswordResetService>();

            // Handler de autenticação de teste (substitui Firebase)
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

            // Configurar comportamento padrão do RateLimiter (permitir tudo por padrão)
            RateLimiter.IsAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(true));

            // Configurar TenantDirectory padrão (slug válido)
            TenantDirectory.ResolveSlugAsync(DefaultTenantSlug, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<TenantResolutionResult?>(new TenantResolutionResult
                {
                    TenantId = DefaultTenantId,
                    IdentityTenantId = DefaultFirebaseTenant
                }));

            // Slug desconhecido retorna null (404)
            TenantDirectory.ResolveSlugAsync(
                    Arg.Is<string>(s => s != DefaultTenantSlug),
                    Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<TenantResolutionResult?>(null));

            // Configurar UserDirectory padrão
            UserDirectory.FindUserAsync(
                    Arg.Any<string>(),
                    DefaultTenantId,
                    Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<UserDirectoryResult?>(new UserDirectoryResult
                {
                    UserId = DefaultUserId,
                    Email = "user@acme.com",
                    IsActive = true,
                    Roles = ["viewer"],
                    Memberships = MembershipSet.Empty,
                    SignInProvider = "password"
                }));

            // AuditEmitter não faz nada por padrão
            AuditEmitter.EmitAsync(
                    Arg.Any<string>(),
                    Arg.Any<Guid>(),
                    Arg.Any<Guid>(),
                    Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            // IdentityProvider: RevokeRefreshTokens não falha por padrão
            IdentityProvider.RevokeRefreshTokensAsync(
                    Arg.Any<Guid>(),
                    Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
        });

        return base.CreateHost(builder);
    }
}

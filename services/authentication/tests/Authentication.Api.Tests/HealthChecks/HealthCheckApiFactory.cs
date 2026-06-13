using Authentication.Application.Ports;
using Authentication.Application.Services;
using PortHealthStatus = Authentication.Application.Ports.Results.HealthStatus;
using Authentication.Infrastructure.HealthChecks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;

namespace Authentication.Api.Tests.HealthChecks;

/// <summary>
/// WebApplicationFactory especializada para testes de health check.
///
/// Registra os health checks de IdP e Redis com mocks controláveis via propriedades
/// booleanas. Remove registros duplicados que surgiriam do Program.cs original.
///
/// Mapeia: TASK-21, RNF 3.2.
/// </summary>
public sealed class HealthCheckApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Controla se o IdP responde como Healthy (true) ou Unhealthy (false).</summary>
    public bool IdpHealthy { get; set; } = true;

    /// <summary>Controla se o Redis responde em latência aceitável (true) ou não (false).</summary>
    public bool RedisHealthy { get; set; } = true;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Mocks das portas necessárias
            var idp = BuildIdpMock();
            var tenantDir = Substitute.For<ITenantDirectory>();
            var userDir = Substitute.For<IUserDirectory>();
            var rateLimiter = Substitute.For<IRateLimiter>();
            var emailSender = Substitute.For<IEmailSender>();
            var auditEmitter = Substitute.For<IAuditEventEmitter>();

            services.RemoveAll<ITenantDirectory>();
            services.RemoveAll<IIdentityProvider>();
            services.RemoveAll<IUserDirectory>();
            services.RemoveAll<IRateLimiter>();
            services.RemoveAll<IEmailSender>();
            services.RemoveAll<IAuditEventEmitter>();

            services.AddSingleton(tenantDir);
            services.AddSingleton(idp);
            services.AddSingleton(userDir);
            services.AddSingleton(rateLimiter);
            services.AddSingleton(emailSender);
            services.AddSingleton(auditEmitter);

            // Redis mock para o health check
            services.AddSingleton<IConnectionMultiplexer>(_ => BuildRedisMock());

            // Cache e opções
            services.AddMemoryCache();
            services.Configure<PasswordResetOptions>(opts => opts.ConstantDelayMs = 0);

            // Serviços de aplicação
            services.AddScoped<SessionTokenValidator>();
            services.AddScoped<AuthContextComposer>();
            services.AddScoped<SessionRevocationService>();
            services.AddScoped<InviteActivationService>();
            services.AddScoped<PasswordResetService>();

            // Comportamento padrão de stubs
            rateLimiter.IsAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(true);
            auditEmitter.EmitAsync(
                Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            // Remover health check registrations duplicadas do Program.cs
            // para evitar InvalidOperationException por nomes duplicados
            services.RemoveAll<IHealthCheckPublisher>();
            // Sobrescreve HealthCheckServiceOptions para limpar as registrations
            services.Configure<HealthCheckServiceOptions>(opts => opts.Registrations.Clear());

            // Re-registrar health checks com mocks injetados
            services.AddHealthChecks()
                .AddCheck<IdentityProviderHealthCheck>("identity_provider", tags: ["readiness"])
                .AddCheck<RedisHealthCheck>("redis", tags: ["readiness"]);
        });

        return base.CreateHost(builder);
    }

    private IIdentityProvider BuildIdpMock()
    {
        var idp = Substitute.For<IIdentityProvider>();
        idp.HealthCheckAsync(Arg.Any<CancellationToken>())
           .Returns(_ => Task.FromResult(
               IdpHealthy
                   ? PortHealthStatus.Healthy
                   : PortHealthStatus.Unhealthy));
        return idp;
    }

    private IConnectionMultiplexer BuildRedisMock()
    {
        var redis = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);

        db.PingAsync(Arg.Any<CommandFlags>())
          .Returns(_ => Task.FromResult(
              RedisHealthy
                  ? TimeSpan.FromMilliseconds(1)
                  : TimeSpan.FromSeconds(10)));

        return redis;
    }
}

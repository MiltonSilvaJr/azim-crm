using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Organization.Application.Ports;
using Organization.Infrastructure.Adapters;
using Organization.Infrastructure.Adapters.Identity;
using Organization.Infrastructure.Cache;
using Organization.Infrastructure.Messaging;
using Organization.Infrastructure.Observability;
using Organization.Infrastructure.Persistence;
using Organization.Infrastructure.Repositories;
using StackExchange.Redis;

namespace Organization.Api.DependencyInjection;

/// <summary>
/// Extensões de registro de serviços de Infrastructure na composição raiz (Program.cs).
/// Centraliza o registro de DbContext, repositórios, cache, messaging e adapters externos.
/// </summary>
public static class InfrastructureExtensions
{
    /// <summary>
    /// Registra todos os serviços de Infrastructure no contêiner de DI.
    /// </summary>
    /// <param name="services">Coleção de serviços.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    /// <returns>A mesma coleção para encadeamento.</returns>
    public static IServiceCollection AddOrganizationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Persistência ──────────────────────────────────────────────────────
        services.AddScoped<TenantContextAccessor>();

        services.AddDbContext<OrganizationDbContext>((sp, options) =>
        {
            var connectionString = configuration.GetConnectionString("Organization")
                ?? "Host=localhost;Database=organization;Username=postgres;Password=postgres";

            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(3);
                npgsql.CommandTimeout(30);
            });
        });

        // OrganizationDbContext implementa IDatabaseContext
        services.AddScoped<IDatabaseContext>(sp =>
            sp.GetRequiredService<OrganizationDbContext>());

        // ── Repositórios ──────────────────────────────────────────────────────
        services.AddScoped<IBusinessUnitRepository, BusinessUnitRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserInvitationRepository, UserInvitationRepository>();
        services.AddScoped<ITenantAdminCounter, TenantAdminCounterAdapter>();

        // ── Redis / Cache ─────────────────────────────────────────────────────
        var redisConnection = configuration.GetConnectionString("Redis")
            ?? "localhost:6379";

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnection));

        services.Configure<MembershipCacheOptions>(
            configuration.GetSection("MembershipCache"));

        services.AddScoped<IMembershipCache, RedisMembershipCache>();

        // ── Messaging ─────────────────────────────────────────────────────────
        services.AddScoped<IEventOutbox, EfCoreEventOutbox>();
        services.AddScoped<IInboxStore, EfCoreInboxStore>();
        services.AddSingleton<IPubSubPublisher, LoggingPubSubPublisher>();

        services.Configure<OutboxWorkerOptions>(
            configuration.GetSection("OutboxWorker"));

        services.AddHostedService<OutboxWorker>();

        // ── Adapters externos ─────────────────────────────────────────────────
        services.Configure<IdentityPlatformOptions>(
            configuration.GetSection("IdentityPlatform"));

        // HttpClient para IdentityPlatformProvisioner com resiliência Polly
        services.AddHttpClient<IIdentityProvisioner, IdentityPlatformProvisioner>(client =>
        {
            var baseUrl = configuration["IdentityPlatform:BaseUrl"] ?? "https://identitytoolkit.googleapis.com";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        // Stubs in-process para MVP
        services.AddScoped<IOpportunityCounter, OpportunityCounterStub>();
        services.AddScoped<IActivityCounter, ActivityCounterStub>();

        // IClock
        services.AddSingleton<IClock, SystemClock>();

        // ITokenHasher — substituído por HmacSha256TokenHasher na TASK-26 (Onda 6)
        services.AddSingleton<ITokenHasher, Sha256TokenHasher>();

        // ── Observabilidade ───────────────────────────────────────────────────
        services.AddSingleton<OrganizationMetrics>();
        services.AddSingleton<IOrganizationMetrics, OrganizationMetricsAdapter>();
        services.AddSingleton<ILastTenantAdminAlertService, LastTenantAdminAlertService>();

        // Health checks concretos registrados no DI
        services.AddScoped<PostgresHealthCheck>();
        services.AddSingleton<RedisHealthCheck>();

        return services;
    }

    /// <summary>
    /// Registra os health checks do módulo Organization.
    /// <para>
    /// - <c>/health/live</c>: registrado em Program.cs sem dependências externas (liveness).
    /// - <c>/health/ready</c>: registrado aqui com Postgres e Redis (readiness).
    /// </para>
    /// <para>
    /// <see cref="PostgresHealthCheck"/> requer <see cref="OrganizationDbContext"/> (Scoped),
    /// portanto é registrado via factory que cria um scope isolado por execução.
    /// </para>
    /// </summary>
    public static IHealthChecksBuilder AddOrganizationHealthChecks(
        this IHealthChecksBuilder builder)
    {
        // PostgresHealthCheck usa OrganizationDbContext (Scoped) — factory com scope
        builder.Add(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration(
            "postgres",
            sp =>
            {
                var scope = sp.CreateScope();
                return scope.ServiceProvider.GetRequiredService<PostgresHealthCheck>();
            },
            HealthStatus.Unhealthy,
            tags: ["ready"]));

        // RedisHealthCheck usa IConnectionMultiplexer (Singleton) — simples
        builder.AddCheck<RedisHealthCheck>(
            "redis",
            HealthStatus.Unhealthy,
            tags: ["ready"]);

        return builder;
    }
}

/// <summary>Implementação de <see cref="IClock"/> baseada no relógio do sistema.</summary>
internal sealed class SystemClock : IClock
{
    /// <inheritdoc/>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>
/// Implementação simples de <see cref="ITokenHasher"/> usando SHA-256 (MVP sem pepper).
/// Substituir por HmacSha256 com pepper via Secret Manager na Onda 6 (TASK-26).
/// </summary>
internal sealed class Sha256TokenHasher : ITokenHasher
{
    /// <inheritdoc/>
    public (string PlainToken, string Hash) GenerateToken()
    {
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        var plain = Convert.ToBase64String(bytes);
        var hash = Hash(plain);
        return (plain, hash);
    }

    /// <inheritdoc/>
    public string Hash(string plainToken)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

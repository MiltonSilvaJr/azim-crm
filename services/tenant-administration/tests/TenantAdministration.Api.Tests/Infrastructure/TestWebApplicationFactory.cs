using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using TenantAdministration.Application.Ports;
using TenantAdministration.Domain.Aggregates;

namespace TenantAdministration.Api.Tests.Infrastructure;

/// <summary>
/// Factory WebApplicationFactory para testes de API do módulo TenantAdministration.
/// Substitui dependências de infraestrutura por fakes/mocks para isolar a camada de API.
/// O papel do usuário é resolvido via header <c>X-Test-Role</c> pelo <see cref="TestCurrentUserContext"/>,
/// eliminando dependência do pipeline JWT nos testes.
/// </summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public ITenantRepository TenantRepository { get; } = Substitute.For<ITenantRepository>();
    public ITenantProvisioningSaga ProvisioningSaga { get; } = Substitute.For<ITenantProvisioningSaga>();
    public IBrandingAssetStorage AssetStorage { get; } = Substitute.For<IBrandingAssetStorage>();
    public ICdnInvalidator CdnInvalidator { get; } = Substitute.For<ICdnInvalidator>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // ICurrentUserContext: substituído por TestCurrentUserContext que lê X-Test-Role do header.
            // Isso elimina a dependência do pipeline JWT (TestAuthHandler) e garante que o
            // AuthorizationBehavior funcione corretamente nos testes sem necessidade de JWT real.
            services.AddScoped<ICurrentUserContext, TestCurrentUserContext>();
            services.AddScoped<ITenantContext, TestTenantContext>();

            // Mocks de ports de infraestrutura
            services.AddScoped(_ => TenantRepository);
            services.AddScoped(_ => ProvisioningSaga);
            services.AddScoped(_ => AssetStorage);
            services.AddScoped(_ => CdnInvalidator);

            // IUnitOfWork, IIdempotencyStore e IEventOutbox como no-ops para testes de API
            services.AddScoped<IUnitOfWork>(_ => new NoOpUnitOfWork());
            services.AddScoped<IIdempotencyStore>(_ => new NoOpIdempotencyStore());
            services.AddScoped<IEventOutbox>(_ => new NoOpEventOutbox());
            services.AddSingleton<IClock>(_ => new FakeClock(DateTimeOffset.UtcNow));
        });
    }

    /// <summary>
    /// Cria cliente HTTP autenticado como Platform Operator.
    /// </summary>
    public HttpClient CreatePlatformOperatorClient(Guid? tenantId = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "PlatformOperator");
        if (tenantId.HasValue)
            client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId.ToString());
        return client;
    }

    /// <summary>
    /// Cria cliente HTTP autenticado como Tenant Admin para o tenant informado.
    /// </summary>
    public HttpClient CreateTenantAdminClient(Guid tenantId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "TenantAdmin");
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId.ToString());
        return client;
    }

    /// <summary>Cria cliente HTTP anônimo (sem role).</summary>
    public HttpClient CreateAnonymousClient() => CreateClient();

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task BeginAsync(Guid? tenantId, CancellationToken ct = default) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class NoOpIdempotencyStore : IIdempotencyStore
    {
        public ValueTask<string?> GetAsync(string key, CancellationToken ct = default) =>
            ValueTask.FromResult<string?>(null);

        public ValueTask SetAsync(string key, string resultJson, CancellationToken ct = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class NoOpEventOutbox : IEventOutbox
    {
        public Task AppendAsync(
            TenantAdministration.Domain.Events.IDomainEvent domainEvent,
            CancellationToken ct = default) => Task.CompletedTask;

        public Task AppendRangeAsync(
            IEnumerable<TenantAdministration.Domain.Events.IDomainEvent> domainEvents,
            CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}

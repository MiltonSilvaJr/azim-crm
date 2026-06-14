using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Organization.Application.Ports;
using Organization.Infrastructure.Persistence;
using System.Security.Claims;

namespace Organization.Api.Tests.Infrastructure;

/// <summary>
/// Factory de teste para a API de organization.
/// Substitui dependências externas (DB, Redis, adapters) por in-memory/mocks.
/// Permite configurar o usuário autenticado via <see cref="WithUser"/>.
/// </summary>
public sealed class OrganizationApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
{
    private readonly TestAuthOptions _authOptions = new();
    private readonly Guid _defaultTenantId = Guid.NewGuid();

    // Mocks configuráveis pelos testes
    public IBusinessUnitRepository BusinessUnitRepository { get; }
        = Substitute.For<IBusinessUnitRepository>();

    public IUserRepository UserRepository { get; }
        = Substitute.For<IUserRepository>();

    public IUserInvitationRepository InvitationRepository { get; }
        = Substitute.For<IUserInvitationRepository>();

    public IEventOutbox EventOutbox { get; }
        = Substitute.For<IEventOutbox>();

    public IIdentityProvisioner IdentityProvisioner { get; }
        = Substitute.For<IIdentityProvisioner>();

    public ITenantAdminCounter TenantAdminCounter { get; }
        = Substitute.For<ITenantAdminCounter>();

    public IOpportunityCounter OpportunityCounter { get; }
        = Substitute.For<IOpportunityCounter>();

    public IActivityCounter ActivityCounter { get; }
        = Substitute.For<IActivityCounter>();

    public ITokenHasher TokenHasher { get; }
        = Substitute.For<ITokenHasher>();

    public IMembershipCache MembershipCache { get; }
        = Substitute.For<IMembershipCache>();

    public IInboxStore InboxStore { get; }
        = Substitute.For<IInboxStore>();

    public IClock Clock { get; }
        = Substitute.For<IClock>();

    /// <summary>Identificador do tenant padrão usado nos testes.</summary>
    public Guid DefaultTenantId => _defaultTenantId;

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // ── Banco de dados in-memory ──────────────────────────────────────
            services.RemoveAll<DbContextOptions<OrganizationDbContext>>();
            services.RemoveAll<OrganizationDbContext>();

            services.AddDbContext<OrganizationDbContext>((sp, opts) =>
            {
                opts.UseInMemoryDatabase($"org-test-{Guid.NewGuid()}");
            });

            services.RemoveAll<IDatabaseContext>();
            services.AddSingleton<IDatabaseContext, InMemoryDatabaseContext>();

            // ── Substituir repositórios e ports por mocks ─────────────────────
            services.RemoveAll<IBusinessUnitRepository>();
            services.AddSingleton(BusinessUnitRepository);

            services.RemoveAll<IUserRepository>();
            services.AddSingleton(UserRepository);

            services.RemoveAll<IUserInvitationRepository>();
            services.AddSingleton(InvitationRepository);

            services.RemoveAll<IEventOutbox>();
            services.AddSingleton(EventOutbox);

            services.RemoveAll<IIdentityProvisioner>();
            services.AddSingleton(IdentityProvisioner);

            services.RemoveAll<ITenantAdminCounter>();
            services.AddSingleton(TenantAdminCounter);

            services.RemoveAll<IOpportunityCounter>();
            services.AddSingleton(OpportunityCounter);

            services.RemoveAll<IActivityCounter>();
            services.AddSingleton(ActivityCounter);

            services.RemoveAll<ITokenHasher>();
            services.AddSingleton(TokenHasher);

            services.RemoveAll<IMembershipCache>();
            services.AddSingleton(MembershipCache);

            services.RemoveAll<IInboxStore>();
            services.AddSingleton(InboxStore);

            services.RemoveAll<IClock>();
            services.AddSingleton(Clock);

            // ── Redis — remover conexão real ──────────────────────────────────
            services.RemoveAll<StackExchange.Redis.IConnectionMultiplexer>();

            // ── Remover OutboxWorker (hosted service) ─────────────────────────
            services.RemoveAll<Microsoft.Extensions.Hosting.IHostedService>();

            // ── Auth handler de teste ─────────────────────────────────────────
            services.AddSingleton(_authOptions);

            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthScheme.Name;
                    options.DefaultChallengeScheme = TestAuthScheme.Name;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthScheme.Name, _ => { });
        });
    }

    /// <summary>
    /// Cria um cliente HTTP autenticado com o papel especificado.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant.</param>
    /// <param name="userId">Identificador do usuário.</param>
    /// <param name="buId">Identificador da BU para bu_role.</param>
    /// <param name="role">Papel (TAdmin, GestorBU, Vendedor, Viewer).</param>
    public HttpClient CreateClientAs(Guid tenantId, Guid userId, Guid buId, string role)
    {
        _authOptions.CurrentClaims = BuildClaims(tenantId, userId, buId, role);
        return CreateClient();
    }

    /// <summary>Cria um cliente HTTP não autenticado.</summary>
    public HttpClient CreateAnonymousClient()
    {
        _authOptions.CurrentClaims = null;
        return CreateClient();
    }

    private static IEnumerable<Claim> BuildClaims(
        Guid tenantId, Guid userId, Guid buId, string role)
    {
        return
        [
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("sub", userId.ToString()),
            new Claim("bu_role", $"{buId}:{role}"),
        ];
    }

    /// <inheritdoc/>
    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
    }
}

using Digest.Infrastructure.Persistence;
using Digest.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Digest.Infrastructure.Tests.Fixtures;

/// <summary>
/// Fixture de Testcontainers especializada em testes de isolamento RLS (TASK-15).
/// Cria DigestDbContext com <see cref="TenantConnectionInterceptor"/> registrado.
/// O container usa o mesmo superuser para aplicar RLS, e a fixture expõe
/// helpers que verificam o comportamento falha-fechada sem SET.
/// </summary>
public sealed class PostgresRlsFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;

    public string ConnectionString => _container.GetConnectionString();

    public PostgresRlsFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithDatabase("digest_rls_test")
            .WithUsername("digest_rls_worker")
            .WithPassword("digest_rls_pw")
            .WithImage("postgres:16-alpine")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await SetupSchemaAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>
    /// Cria DbContext com interceptor e tenant setado.
    /// </summary>
    public DigestDbContext CreateContextWithInterceptor(Guid tenantId)
    {
        var tenantCtx = new TenantContext();
        if (tenantId != Guid.Empty)
            tenantCtx.SetTenant(tenantId);

        var interceptor = new TenantConnectionInterceptor(tenantCtx);

        var options = new DbContextOptionsBuilder<DigestDbContext>()
            .UseNpgsql(ConnectionString, o => o.UseNodaTime())
            .AddInterceptors(interceptor)
            .Options;

        return new DigestDbContext(options, tenantCtx);
    }

    /// <summary>
    /// Cria DbContext com interceptor mas SEM setar o tenant (empty = falha-fechada).
    /// </summary>
    public DigestDbContext CreateContextWithInterceptorNoTenant()
        => CreateContextWithInterceptor(Guid.Empty);

    /// <summary>
    /// Cria DbContext SEM interceptor e SEM Global Query Filter (para inserção administrativa de dados de teste).
    /// Usado apenas para setup de dados de teste — nunca em produção.
    /// </summary>
    public DigestDbContext CreateAdminContext()
    {
        var tenantCtx = new TenantContext(); // empty

        var options = new DbContextOptionsBuilder<DigestDbContext>()
            .UseNpgsql(ConnectionString, o => o.UseNodaTime())
            .Options;

        return new DigestDbContext(options, tenantCtx);
    }

    // ---------------------------------------------------------------
    // Setup do schema + RLS
    // ---------------------------------------------------------------

    /// <summary>
    /// String de conexão com um usuário NOSUPERUSER para testes de RLS.
    /// O usuário owner não bypass FORCE ROW LEVEL SECURITY apenas se não for superusuário.
    /// Criamos um role 'digest_app_user' com apenas CONNECT para testar o RLS corretamente.
    /// </summary>
    public string RlsUserConnectionString { get; private set; } = string.Empty;

    private async Task SetupSchemaAsync()
    {
        await using var adminCtx = CreateAdminContext();

        // Cria schema via EnsureCreated (usa o owner digest_rls_worker)
        await adminCtx.Database.EnsureCreatedAsync();

        // Cria role NOSUPERUSER para testes de RLS (não é owner das tabelas)
        await adminCtx.Database.ExecuteSqlRawAsync(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'digest_app_user') THEN
        CREATE ROLE digest_app_user LOGIN PASSWORD 'app_pw' NOSUPERUSER NOCREATEDB NOCREATEROLE;
    END IF;
END;
$$;
GRANT CONNECT ON DATABASE digest_rls_test TO digest_app_user;
GRANT SELECT, INSERT, UPDATE ON email_digest_logs TO digest_app_user;
GRANT SELECT, INSERT, UPDATE ON digest_action_tokens TO digest_app_user;
");

        // String de conexão para o usuário de aplicação (não owner — RLS se aplica plenamente)
        RlsUserConnectionString = _container.GetConnectionString()
            .Replace("Username=digest_rls_worker", "Username=digest_app_user")
            .Replace("Password=digest_rls_pw", "Password=app_pw");

        // Aplica RLS (inline para não depender de arquivo)
        await adminCtx.Database.ExecuteSqlRawAsync(@"
ALTER TABLE email_digest_logs ENABLE ROW LEVEL SECURITY;
ALTER TABLE email_digest_logs FORCE ROW LEVEL SECURITY;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_policies
        WHERE tablename = 'email_digest_logs' AND policyname = 'p_email_digest_logs_tenant'
    ) THEN
        CREATE POLICY p_email_digest_logs_tenant ON email_digest_logs
            USING (tenant_id = current_setting('app.current_tenant', true)::uuid)
            WITH CHECK (tenant_id = current_setting('app.current_tenant', true)::uuid);
    END IF;
END;
$$;

ALTER TABLE digest_action_tokens ENABLE ROW LEVEL SECURITY;
ALTER TABLE digest_action_tokens FORCE ROW LEVEL SECURITY;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_policies
        WHERE tablename = 'digest_action_tokens' AND policyname = 'p_digest_action_tokens_tenant'
    ) THEN
        CREATE POLICY p_digest_action_tokens_tenant ON digest_action_tokens
            USING (tenant_id = current_setting('app.current_tenant', true)::uuid)
            WITH CHECK (tenant_id = current_setting('app.current_tenant', true)::uuid);
    END IF;
END;
$$;
");
    }
}

[CollectionDefinition("PostgresRls")]
public sealed class PostgresRlsCollection : ICollectionFixture<PostgresRlsFixture>;

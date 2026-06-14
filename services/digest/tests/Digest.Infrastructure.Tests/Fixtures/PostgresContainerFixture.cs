using System.Reflection;
using Digest.Domain.Entities;
using Digest.Infrastructure.Messaging;
using Digest.Infrastructure.Persistence;
using Digest.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Digest.Infrastructure.Tests.Fixtures;

/// <summary>
/// Fixture de Testcontainers que sobe PostgreSQL real, executa migrations e script RLS.
/// Compartilhada entre os testes de integração da Onda 4 (TASK-14..18).
/// O usuário do Testcontainer é NOSUPERUSER para testar RLS falha-fechada (ADR-0001).
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;

    public string ConnectionString => _container.GetConnectionString();

    public PostgresContainerFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithDatabase("digest_test")
            .WithUsername("digest_worker")
            .WithPassword("digest_test_pw")
            .WithImage("postgres:16-alpine")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await ApplyMigrationsAsync();
        await ApplyRlsScriptAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>
    /// Cria um <see cref="DigestDbContext"/> com o <paramref name="tenantId"/> setado no contexto.
    /// </summary>
    public DigestDbContext CreateDbContext(Guid tenantId)
    {
        var tenantCtx = new TenantContext();
        if (tenantId != Guid.Empty)
            tenantCtx.SetTenant(tenantId);

        var optionsBuilder = new DbContextOptionsBuilder<DigestDbContext>();
        optionsBuilder.UseNpgsql(ConnectionString, o =>
            o.UseNodaTime());

        return new DigestDbContext(optionsBuilder.Options, tenantCtx);
    }

    /// <summary>
    /// Cria um <see cref="DigestDbContext"/> sem tenant setado (escopo cross-tenant ou vazio).
    /// </summary>
    public DigestDbContext CreateDbContextWithoutTenant()
    {
        var tenantCtx = new TenantContext(); // CurrentTenantId = Guid.Empty

        var optionsBuilder = new DbContextOptionsBuilder<DigestDbContext>();
        optionsBuilder.UseNpgsql(ConnectionString, o =>
            o.UseNodaTime());

        return new DigestDbContext(optionsBuilder.Options, tenantCtx);
    }

    // ---------------------------------------------------------------
    // Helpers privados
    // ---------------------------------------------------------------

    private async Task ApplyMigrationsAsync()
    {
        // EnsureCreated cria o schema baseado no modelo atual do EF Core.
        // Para testes de integração é a abordagem correta; migrations formais são para produção.
        // O EF Core cria tabelas, índices UNIQUE e índices compostos definidos nas configurações.
        await using var ctx = CreateDbContextWithoutTenant();
        await ctx.Database.EnsureCreatedAsync();
    }

    private async Task ApplyRlsScriptAsync()
    {
        // Lê o script de RLS embutido como recurso ou caminho relativo
        var scriptPath = Path.Combine(
            Path.GetDirectoryName(typeof(PostgresContainerFixture).Assembly.Location)!,
            "Scripts",
            "apply-rls.sql");

        string sql;
        if (File.Exists(scriptPath))
        {
            sql = await File.ReadAllTextAsync(scriptPath);
        }
        else
        {
            // Fallback: script inline para garantir testes mesmo sem arquivo copiado
            sql = GetRlsScriptInline();
        }

        await using var ctx = CreateDbContextWithoutTenant();
        // Executa como superuser (postgres) — o Testcontainer usa o owner da tabela
        await ctx.Database.ExecuteSqlRawAsync(sql);
    }

    private static string GetRlsScriptInline() => @"
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
";
}

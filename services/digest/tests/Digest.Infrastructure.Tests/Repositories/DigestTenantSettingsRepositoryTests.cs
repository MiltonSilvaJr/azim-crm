using Digest.Infrastructure.Persistence;
using Digest.Infrastructure.Repositories;
using Digest.Infrastructure.Security;
using Digest.Infrastructure.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Digest.Infrastructure.Tests.Repositories;

/// <summary>
/// Testes de integração para <see cref="DigestTenantSettingsRepository"/> (VAL-ACT-02).
/// Verifica leitura do TTL por tenant, retorno null quando ausente e isolamento RLS.
/// Usa Testcontainers + PostgreSQL real.
/// </summary>
public sealed class DigestTenantSettingsRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private string _connectionString = string.Empty;

    public DigestTenantSettingsRepositoryTests()
    {
        _container = new PostgreSqlBuilder()
            .WithDatabase("digest_settings_test")
            .WithUsername("digest_settings_worker")
            .WithPassword("digest_settings_pw")
            .WithImage("postgres:16-alpine")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
        await SetupSchemaAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private DigestDbContext CreateContext(Guid tenantId)
    {
        var tenantCtx = new TenantContext();
        if (tenantId != Guid.Empty)
            tenantCtx.SetTenant(tenantId);

        var interceptor = new TenantConnectionInterceptor(tenantCtx);

        var options = new DbContextOptionsBuilder<DigestDbContext>()
            .UseNpgsql(_connectionString, o => o.UseNodaTime())
            .AddInterceptors(interceptor)
            .Options;

        return new DigestDbContext(options, tenantCtx);
    }

    private DigestDbContext CreateAdminContext()
    {
        var tenantCtx = new TenantContext();
        var options = new DbContextOptionsBuilder<DigestDbContext>()
            .UseNpgsql(_connectionString, o => o.UseNodaTime())
            .Options;
        return new DigestDbContext(options, tenantCtx);
    }

    private async Task SetupSchemaAsync()
    {
        await using var ctx = CreateAdminContext();
        await ctx.Database.EnsureCreatedAsync();

        // Aplica RLS para digest_tenant_settings (inline, idempotente)
        await ctx.Database.ExecuteSqlRawAsync(@"
ALTER TABLE digest_tenant_settings ENABLE ROW LEVEL SECURITY;
ALTER TABLE digest_tenant_settings FORCE ROW LEVEL SECURITY;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_policies
        WHERE tablename = 'digest_tenant_settings'
          AND policyname = 'p_digest_tenant_settings_tenant'
    ) THEN
        CREATE POLICY p_digest_tenant_settings_tenant ON digest_tenant_settings
            USING (tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::uuid)
            WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', TRUE), '')::uuid);
    END IF;
END;
$$;
");
    }

    private async Task InsertSettingAsync(Guid tenantId, int ttlHours)
    {
        // Insere via SQL direto (sem RLS) para preparar dados de teste
        await using var ctx = CreateAdminContext();
        await ctx.Database.ExecuteSqlRawAsync(
            $"INSERT INTO digest_tenant_settings (tenant_id, action_token_ttl_hours, created_at, updated_at) " +
            $"VALUES ('{tenantId}', {ttlHours}, now(), now()) " +
            $"ON CONFLICT (tenant_id) DO UPDATE SET action_token_ttl_hours = EXCLUDED.action_token_ttl_hours");
    }

    // ---------------------------------------------------------------
    // Testes
    // ---------------------------------------------------------------

    [Fact(DisplayName = "GetActionTokenTtlHoursAsync retorna o TTL configurado para o tenant")]
    public async Task GetActionTokenTtlHoursAsync_ReturnsTenantTtl_WhenSettingExists()
    {
        var tenantId = Guid.NewGuid();
        await InsertSettingAsync(tenantId, 72);

        await using var ctx = CreateContext(tenantId);
        var repo = new DigestTenantSettingsRepository(ctx);

        var result = await repo.GetActionTokenTtlHoursAsync(tenantId);

        result.Should().Be(72, because: "o setting configurado para o tenant deve ser retornado (VAL-ACT-02)");
    }

    [Fact(DisplayName = "GetActionTokenTtlHoursAsync retorna null quando tenant não possui setting")]
    public async Task GetActionTokenTtlHoursAsync_ReturnsNull_WhenSettingAbsent()
    {
        var tenantId = Guid.NewGuid();
        // Não insere nenhum setting para este tenant

        await using var ctx = CreateContext(tenantId);
        var repo = new DigestTenantSettingsRepository(ctx);

        var result = await repo.GetActionTokenTtlHoursAsync(tenantId);

        result.Should().BeNull(
            because: "ausência de setting deve retornar null — fallback para default 48h na Application (VAL-ACT-02)");
    }

    [Fact(DisplayName = "GetActionTokenTtlHoursAsync retorna null para tenant diferente — Global Query Filter (ADR-0001)")]
    public async Task GetActionTokenTtlHoursAsync_ReturnsNull_ForDifferentTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // Insere setting para tenantA
        await InsertSettingAsync(tenantA, 96);

        // Consulta com contexto do tenantB — Global Query Filter deve impedir acesso ao setting do tenantA
        await using var ctx = CreateContext(tenantB);
        var repo = new DigestTenantSettingsRepository(ctx);

        var result = await repo.GetActionTokenTtlHoursAsync(tenantB);

        result.Should().BeNull(
            because: "Global Query Filter deve impedir acesso cross-tenant ao setting (ADR-0001)");
    }

    [Fact(DisplayName = "GetActionTokenTtlHoursAsync rejeita tenant_id vazio")]
    public async Task GetActionTokenTtlHoursAsync_Throws_WhenTenantIdEmpty()
    {
        await using var ctx = CreateContext(Guid.NewGuid());
        var repo = new DigestTenantSettingsRepository(ctx);

        var act = async () => await repo.GetActionTokenTtlHoursAsync(Guid.Empty);

        await act.Should().ThrowAsync<ArgumentException>(
            because: "tenant_id vazio deve ser rejeitado");
    }

    [Fact(DisplayName = "RLS fail-closed: sem SET app.current_tenant, nenhum setting é acessível")]
    public async Task Rls_FailClosed_WhenNoTenantSet()
    {
        var tenantId = Guid.NewGuid();
        await InsertSettingAsync(tenantId, 48);

        // Contexto sem tenant (empty) — RLS deve bloquear
        await using var ctx = CreateContext(Guid.Empty);
        var repo = new DigestTenantSettingsRepository(ctx);

        // Tenta consultar qualquer setting — deve retornar null (zero linhas por RLS + Global Query Filter)
        var result = await repo.GetActionTokenTtlHoursAsync(tenantId);

        result.Should().BeNull(because: "RLS fail-closed deve bloquear acesso sem tenant definido (ADR-0001)");
    }
}

using Digest.Domain.Entities;
using Digest.Domain.ValueObjects;
using Digest.Infrastructure.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Xunit;

namespace Digest.Infrastructure.Tests.Security;

/// <summary>
/// Testes de isolamento RLS falha-fechada (TASK-15 — gate obrigatório de CI, RNF 1.3).
/// Verifica três cenários com Testcontainers + PostgreSQL real:
/// (a) com SET correto → acessa somente dados do tenant;
/// (b) sem SET → 0 linhas (falha-fechada);
/// (c) com tenant_id de outro tenant → não acessa dados do tenant A.
/// </summary>
[Collection("PostgresRls")]
public sealed class RlsIsolationTests : IAsyncLifetime
{
    private readonly PostgresRlsFixture _fixture;
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DigestDate _digestDate = new(new LocalDate(2026, 6, 14));

    public RlsIsolationTests(PostgresRlsFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Insere dados de teste para ambos os tenants via admin context (sem RLS)
        await using var adminCtx = _fixture.CreateAdminContext();

        // Usa SET via SQL direto para contornar RLS no admin context de setup
        await adminCtx.Database.ExecuteSqlAsync($"SET app.current_tenant = '{_tenantA}'");
        var logA = EmailDigestLog.Schedule(_tenantA, _userId, _digestDate, null);
        adminCtx.EmailDigestLogs.Add(logA);
        await adminCtx.SaveChangesAsync();

        await adminCtx.Database.ExecuteSqlAsync($"SET app.current_tenant = '{_tenantB}'");
        var logB = EmailDigestLog.Schedule(_tenantB, _userId, _digestDate, null);
        adminCtx.EmailDigestLogs.Add(logB);
        await adminCtx.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------
    // Cenário (a): com SET correto → acessa somente dados do tenant correto
    // ---------------------------------------------------------------

    [Fact(DisplayName = "RLS(a): interceptor seta app.current_tenant → acessa somente dados do tenant A")]
    public async Task WithInterceptor_TenantA_CanOnlySeeOwnData()
    {
        // Arrange
        await using var ctx = _fixture.CreateContextWithInterceptor(_tenantA);

        // Act
        var logs = await ctx.EmailDigestLogs.ToListAsync();

        // Assert — somente o registro de tenant A (Global Query Filter + RLS)
        logs.Should().HaveCount(1);
        logs[0].TenantId.Should().Be(_tenantA);
    }

    // ---------------------------------------------------------------
    // Cenário (b): sem SET → 0 linhas (RLS falha-fechada — RNF 1.4)
    // ---------------------------------------------------------------

    [Fact(DisplayName = "RLS(b): sem SET app.current_tenant → RLS bloqueia (0 linhas) — falha-fechada")]
    public async Task WithoutSet_RlsBlocksAllRows()
    {
        // Usa o usuário de aplicação NOSUPERUSER (digest_app_user) que não é owner das tabelas.
        // Para esse usuário, FORCE ROW LEVEL SECURITY se aplica plenamente.
        // Sem SET app.current_tenant, a policy retorna false → 0 linhas (RNF 1.4, ADR-0001).
        await using var conn = new Npgsql.NpgsqlConnection(_fixture.RlsUserConnectionString);
        await conn.OpenAsync();

        // NÃO executa SET app.current_tenant — simula sessão sem tenant
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM email_digest_logs";
        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());

        count.Should().Be(0,
            "FORCE ROW LEVEL SECURITY deve bloquear acesso sem app.current_tenant para usuário não-owner (RNF 1.4)");
    }

    // ---------------------------------------------------------------
    // Cenário (c): com tenant B setado → não acessa dados de tenant A
    // ---------------------------------------------------------------

    [Fact(DisplayName = "RLS(c): tenant B não acessa dados de tenant A")]
    public async Task TenantB_CannotAccessTenantAData()
    {
        // Arrange
        await using var ctxB = _fixture.CreateContextWithInterceptor(_tenantB);

        // Act — com tenant B setado, não deve ver registros de tenant A
        var logs = await ctxB.EmailDigestLogs.ToListAsync();

        // Assert
        logs.Should().NotContain(l => l.TenantId == _tenantA,
            "RLS deve impedir tenant B de acessar dados de tenant A (RNF 1.1)");
        logs.Should().OnlyContain(l => l.TenantId == _tenantB);
    }

    // ---------------------------------------------------------------
    // Cenário adicional: SET executado antes do primeiro comando (não depois)
    // ---------------------------------------------------------------

    [Fact(DisplayName = "RLS: interceptor executa SET ao abrir a conexão (antes do primeiro SELECT)")]
    public async Task Interceptor_SetsCurrentTenant_OnConnectionOpen()
    {
        // Arrange
        await using var ctx = _fixture.CreateContextWithInterceptor(_tenantA);

        // Act — primeira query deve já ter o tenant setado
        var count = await ctx.EmailDigestLogs.CountAsync();

        // Assert
        count.Should().Be(1, "interceptor deve ter setado o tenant antes do COUNT");
    }
}

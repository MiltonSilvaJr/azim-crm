using Digest.Domain.Entities;
using Digest.Domain.Enums;
using Digest.Domain.ValueObjects;
using Digest.Infrastructure.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Xunit;

namespace Digest.Infrastructure.Tests.Isolation;

/// <summary>
/// Gate obrigatório de CI: isolamento cross-tenant (TASK-25, RNF 1.3, ADR-0001).
/// Quatro cenários com Testcontainers + PostgreSQL real:
/// (a) tenant A processa digest → EmailDigestLog criado; tenant B não vê os dados de A.
/// (b) DigestActionToken de tenant A é inacessível ao tenant B.
/// (c) Inserção com tenant_id de B rejeitada quando contexto setado para A (RLS WITH CHECK).
/// (d) Sem SET app.current_tenant → 0 linhas (falha-fechada — RNF 1.4, FORCE RLS).
///
/// BLOQUEADOR DE PR: qualquer falha nestes testes impede o merge (RNF 1.3).
/// Categoria: SecurityGate — identificador para filtro no CI.
/// </summary>
[Collection("PostgresRls")]
[Trait("Category", "SecurityGate")]
public sealed class CrossTenantIsolationGateTests : IAsyncLifetime
{
    private readonly PostgresRlsFixture _fixture;
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _userB = Guid.NewGuid();
    private readonly DigestDate _dateA = new(new LocalDate(2026, 6, 15));
    private readonly DigestDate _dateB = new(new LocalDate(2026, 6, 16));

    public CrossTenantIsolationGateTests(PostgresRlsFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Setup: insere dados para dois tenants distintos via admin context (sem RLS)
        await using var adminCtx = _fixture.CreateAdminContext();

        // Insere EmailDigestLog para tenant A
        await adminCtx.Database.ExecuteSqlAsync($"SET app.current_tenant = '{_tenantA}'");
        var logA = EmailDigestLog.Schedule(_tenantA, _userA, _dateA, null);
        adminCtx.EmailDigestLogs.Add(logA);
        await adminCtx.SaveChangesAsync();

        // Insere EmailDigestLog para tenant B
        await adminCtx.Database.ExecuteSqlAsync($"SET app.current_tenant = '{_tenantB}'");
        var logB = EmailDigestLog.Schedule(_tenantB, _userB, _dateB, null);
        adminCtx.EmailDigestLogs.Add(logB);
        await adminCtx.SaveChangesAsync();

        // Insere DigestActionToken para tenant A
        await adminCtx.Database.ExecuteSqlAsync($"SET app.current_tenant = '{_tenantA}'");
        var tokenA = DigestActionToken.Issue(
            tenantId: _tenantA,
            userId: _userA,
            activityId: Guid.NewGuid(),
            action: ActionType.Complete,
            token: ActionToken.Issue());
        adminCtx.DigestActionTokens.Add(tokenA);
        await adminCtx.SaveChangesAsync();

        // Insere DigestActionToken para tenant B
        await adminCtx.Database.ExecuteSqlAsync($"SET app.current_tenant = '{_tenantB}'");
        var tokenB = DigestActionToken.Issue(
            tenantId: _tenantB,
            userId: _userB,
            activityId: Guid.NewGuid(),
            action: ActionType.Reschedule,
            token: ActionToken.Issue());
        adminCtx.DigestActionTokens.Add(tokenB);
        await adminCtx.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------
    // Cenário (a): EmailDigestLog de tenant A inacessível ao tenant B
    // ---------------------------------------------------------------

    [Fact(DisplayName = "CI Gate(a): tenant B não vê EmailDigestLog de tenant A via RLS")]
    public async Task Scenario_A_TenantB_CannotSeeDigestLogsOfTenantA()
    {
        // Arrange: contexto com interceptor setado para tenant B
        await using var ctxB = _fixture.CreateContextWithInterceptor(_tenantB);

        // Act
        var logs = await ctxB.EmailDigestLogs.ToListAsync();

        // Assert: somente logs de B são visíveis
        logs.Should().NotContain(l => l.TenantId == _tenantA,
            "RLS deve impedir tenant B de acessar registros de email_digest_logs do tenant A (RNF 1.1)");
        logs.Should().OnlyContain(l => l.TenantId == _tenantB);
    }

    // ---------------------------------------------------------------
    // Cenário (b): DigestActionToken de tenant A inacessível ao tenant B
    // ---------------------------------------------------------------

    [Fact(DisplayName = "CI Gate(b): DigestActionToken de tenant A é inacessível ao tenant B")]
    public async Task Scenario_B_TenantB_CannotSeeActionTokensOfTenantA()
    {
        // Arrange: contexto com interceptor setado para tenant B
        await using var ctxB = _fixture.CreateContextWithInterceptor(_tenantB);

        // Act
        var tokens = await ctxB.DigestActionTokens.ToListAsync();

        // Assert
        tokens.Should().NotContain(t => t.TenantId == _tenantA,
            "RLS deve impedir tenant B de acessar digest_action_tokens do tenant A (RNF 1.1)");
        tokens.Should().OnlyContain(t => t.TenantId == _tenantB);
    }

    // ---------------------------------------------------------------
    // Cenário (c): RLS WITH CHECK impede inserção com tenant_id incorreto
    // ---------------------------------------------------------------

    [Fact(DisplayName = "CI Gate(c): RLS WITH CHECK rejeita inserção com tenant_id de outro tenant")]
    public async Task Scenario_C_RlsWithCheck_RejectsInsertWithWrongTenantId()
    {
        // Arrange: contexto com interceptor setado para tenant A
        // Tenta inserir um registro com tenant_id = B
        await using var ctxA = _fixture.CreateContextWithInterceptor(_tenantA);

        var maliciousLog = EmailDigestLog.Schedule(_tenantB, _userB, _dateB, null);
        ctxA.EmailDigestLogs.Add(maliciousLog);

        // Act
        Func<Task> act = async () => await ctxA.SaveChangesAsync();

        // Assert: RLS WITH CHECK deve rejeitar (exception ou 0 linhas afetadas)
        // PostgreSQL com RLS WITH CHECK lança exceção quando o registro viola a policy
        await act.Should().ThrowAsync<Exception>(
            "RLS WITH CHECK deve rejeitar inserção de registro de tenant B quando contexto é A (ADR-0001, RNF 1.4)");
    }

    // ---------------------------------------------------------------
    // Cenário (d): sem SET app.current_tenant → 0 linhas (falha-fechada)
    // ---------------------------------------------------------------

    [Fact(DisplayName = "CI Gate(d): sem SET app.current_tenant → RLS bloqueia (0 linhas) — falha-fechada")]
    public async Task Scenario_D_WithoutCurrentTenant_RlsBlocksAll()
    {
        // Usa usuário NOSUPERUSER (digest_app_user) — FORCE ROW LEVEL SECURITY se aplica totalmente.
        // Sem SET app.current_tenant, a policy retorna false → 0 linhas (RNF 1.4).
        await using var conn = new Npgsql.NpgsqlConnection(_fixture.RlsUserConnectionString);
        await conn.OpenAsync();

        // NÃO executa SET app.current_tenant — simula sessão sem tenant
        await using var cmdLogs = conn.CreateCommand();
        cmdLogs.CommandText = "SELECT COUNT(*) FROM email_digest_logs";
        var logsCount = Convert.ToInt64(await cmdLogs.ExecuteScalarAsync());

        await using var cmdTokens = conn.CreateCommand();
        cmdTokens.CommandText = "SELECT COUNT(*) FROM digest_action_tokens";
        var tokensCount = Convert.ToInt64(await cmdTokens.ExecuteScalarAsync());

        logsCount.Should().Be(0,
            "FORCE ROW LEVEL SECURITY deve bloquear acesso a email_digest_logs sem app.current_tenant (RNF 1.4)");
        tokensCount.Should().Be(0,
            "FORCE ROW LEVEL SECURITY deve bloquear acesso a digest_action_tokens sem app.current_tenant (RNF 1.4)");
    }
}

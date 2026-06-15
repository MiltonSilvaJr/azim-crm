using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.Services;
using AccountManagement.Domain.Accounts.ValueObjects;
using AccountManagement.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AccountManagement.Infrastructure.Tests.Repository;

/// <summary>
/// Testes de sanidade que provam que a RLS de PostgreSQL — e não apenas o EF Global
/// Query Filter — está efetivamente ativa quando conectado como <c>appuser</c> (NOSUPERUSER).
///
/// Rationale:
/// Superusuários bypassam RLS em PostgreSQL. O fixture anterior criava o container com um
/// usuário que, apesar do nome, era superusuário (POSTGRES_USER do container oficial).
/// Assim, os testes anteriores exercitavam apenas o EF Global Query Filter (lado .NET),
/// mas nunca a policy RLS do banco (segunda camada de defesa — ADR-0001, ADR-0009).
///
/// Estes testes confirmam que a separação está correta:
/// - <see cref="AppConnectionString"/> conecta como <c>appuser</c> (NOSUPERUSER) ⇒ RLS ativa.
/// - <see cref="ConnectionString"/> conecta como superusuário ⇒ RLS bypassada (para seed/admin).
///
/// Cobre:
/// - RLS-01: sem app.current_tenant, appuser não vê nenhuma conta (fail-closed por RLS).
/// - RLS-02: com app.current_tenant correto, appuser vê apenas contas do tenant.
/// - RLS-03: superusuário vê todas as contas mesmo sem app.current_tenant (prova que
///           o bypass de superuser funciona — e por isso o fixture anterior era falso verde).
/// - RLS-04: sem app.current_bu_scope e sem tenant-wide, appuser não vê nenhuma conta
///           do tenant (fail-closed por policy de BU).
///
/// Mapeia: ADR-0001, ADR-0009, VAL-ACC-03, design §14.
/// </summary>
[Collection("PostgresFixture")]
public sealed class RlsEnforcementTests
{
    private readonly PostgresFixture _fixture;
    private readonly NameNormalizer _normalizer = new();

    public RlsEnforcementTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // =========================================================================
    // RLS-01: fail-closed — sem tenant setado, appuser não vê nenhuma conta
    // =========================================================================

    [Fact(DisplayName = "RLS-01: appuser sem app.current_tenant não vê nenhuma conta (RLS fail-closed)")]
    public async Task AppUser_WithoutCurrentTenant_SeesZeroAccounts()
    {
        // Arrange: insere conta via conexão admin (bypass RLS)
        var tenant = Guid.NewGuid();
        var bu = Guid.NewGuid();
        await InsertAccountDirectlyAsync(tenant, bu, $"Empresa RLS-01 {tenant:N}");

        // Act: conecta como appuser SEM setar app.current_tenant nem app.bu_tenant_wide
        // Isso prova que a RLS — não o EF Query Filter — está bloqueando.
        await using var appConn = new NpgsqlConnection(_fixture.AppConnectionString);
        await appConn.OpenAsync();

        // Verifica que a conta existe via conexão admin
        var countViaAdmin = await CountAccountsAsync(tenant, bu, useAdmin: true);
        countViaAdmin.Should().Be(1L, "a conta foi inserida via admin e deve existir no banco");

        // Verifica que appuser não a vê sem tenant setado
        await using var cmdApp = appConn.CreateCommand();
        cmdApp.CommandText = $@"
            SELECT COUNT(*) FROM accounts
            WHERE tenant_id = '{tenant}'";
        var countViaApp = (long)(await cmdApp.ExecuteScalarAsync())!;

        countViaApp.Should().Be(0L,
            "RLS policy fail-closed: sem app.current_tenant, appuser não deve ver nenhuma conta " +
            "(prova que é a RLS do banco bloqueando, não apenas o EF Query Filter)");
    }

    // =========================================================================
    // RLS-02: com tenant correto, appuser vê apenas contas do seu tenant
    // =========================================================================

    [Fact(DisplayName = "RLS-02: appuser com app.current_tenant + bu_tenant_wide vê apenas contas do próprio tenant")]
    public async Task AppUser_WithCurrentTenant_SeesOnlyOwnTenantAccounts()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var bu = Guid.NewGuid();

        await InsertAccountDirectlyAsync(tenantA, bu, $"Empresa TenantA {tenantA:N}");
        await InsertAccountDirectlyAsync(tenantB, bu, $"Empresa TenantB {tenantB:N}");

        // Verifica via admin que ambas as contas existem
        var totalViaAdmin = await CountAllAccountsAsync();
        totalViaAdmin.Should().BeGreaterThanOrEqualTo(2L, "ambas as contas foram inseridas");

        // Conecta como appuser com tenant A setado (tenant-wide para simplificar o escopo de BU)
        await using var appConn = new NpgsqlConnection(_fixture.AppConnectionString);
        await appConn.OpenAsync();

        await using var setCmd = appConn.CreateCommand();
        setCmd.CommandText = $@"
            SET ""app.current_tenant"" = '{tenantA}';
            SET ""app.current_bu_scope"" = '';
            SET ""app.bu_tenant_wide"" = 'true'";
        await setCmd.ExecuteNonQueryAsync();

        await using var queryCmd = appConn.CreateCommand();
        queryCmd.CommandText = $@"
            SELECT COUNT(*) FROM accounts
            WHERE tenant_id = '{tenantA}' OR tenant_id = '{tenantB}'";
        var count = (long)(await queryCmd.ExecuteScalarAsync())!;

        count.Should().Be(1L,
            "RLS de tenant: appuser com tenant A setado deve ver apenas contas do tenant A, " +
            "nunca do tenant B (ADR-0001)");
    }

    // =========================================================================
    // RLS-03: superusuário bypassa RLS — prova que o fixture anterior era falso verde
    // =========================================================================

    [Fact(DisplayName = "RLS-03: superusuário bypassa RLS mesmo sem app.current_tenant (prova do falso verde anterior)")]
    public async Task SuperUser_WithoutCurrentTenant_StillSeesAllAccounts()
    {
        var tenant = Guid.NewGuid();
        var bu = Guid.NewGuid();
        await InsertAccountDirectlyAsync(tenant, bu, $"Empresa RLS-03 {tenant:N}");

        // Superusuário (ConnectionString) não precisa de app.current_tenant para ver dados
        await using var adminConn = new NpgsqlConnection(_fixture.ConnectionString);
        await adminConn.OpenAsync();

        await using var cmd = adminConn.CreateCommand();
        cmd.CommandText = $@"
            SELECT COUNT(*) FROM accounts
            WHERE tenant_id = '{tenant}'";
        var count = (long)(await cmd.ExecuteScalarAsync())!;

        count.Should().Be(1L,
            "superusuário bypassa RLS — vê a conta mesmo sem app.current_tenant. " +
            "Isso demonstra por que o fixture anterior (que usava superuser para queries " +
            "de aplicação) era um falso verde: a policy RLS nunca era avaliada.");
    }

    // =========================================================================
    // RLS-04: fail-closed de BU — sem bu_scope + não-tenant-wide, zero contas
    // =========================================================================

    [Fact(DisplayName = "RLS-04: appuser com tenant correto mas sem bu_scope e não-tenant-wide não vê nenhuma conta")]
    public async Task AppUser_WithTenantButNoBuScope_SeesZeroAccounts()
    {
        var tenant = Guid.NewGuid();
        var bu = Guid.NewGuid();
        await InsertAccountDirectlyAsync(tenant, bu, $"Empresa RLS-04 {tenant:N}");

        // Verifica que a conta existe via admin
        var countViaAdmin = await CountAccountsAsync(tenant, bu, useAdmin: true);
        countViaAdmin.Should().Be(1L, "conta foi inserida");

        // Conecta como appuser: seta tenant correto, mas escopo de BU vazio e não-tenant-wide
        await using var appConn = new NpgsqlConnection(_fixture.AppConnectionString);
        await appConn.OpenAsync();

        await using var setCmd = appConn.CreateCommand();
        setCmd.CommandText = $@"
            SET ""app.current_tenant"" = '{tenant}';
            SET ""app.current_bu_scope"" = '';
            SET ""app.bu_tenant_wide"" = 'false'";
        await setCmd.ExecuteNonQueryAsync();

        await using var queryCmd = appConn.CreateCommand();
        queryCmd.CommandText = $@"
            SELECT COUNT(*) FROM accounts
            WHERE tenant_id = '{tenant}'";
        var count = (long)(await queryCmd.ExecuteScalarAsync())!;

        count.Should().Be(0L,
            "RLS policy de BU fail-closed: tenant correto mas sem bu_scope e não-tenant-wide " +
            "deve retornar zero contas. Prova que a policy bu_scope_isolation está ativa " +
            "no banco — não apenas o EF Query Filter (ADR-0009).");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private Account CreateAccount(Guid tenantId, Guid buId, string name)
    {
        return Account.Create(
            tenantId: tenantId,
            buId: buId,
            name: AccountName.Create(name),
            website: null,
            notes: null,
            normalizer: _normalizer);
    }

    private async Task InsertAccountDirectlyAsync(Guid tenantId, Guid buId, string name)
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        var normalizedNameValue = _normalizer.NormalizeName(name).Value;
        cmd.CommandText = $@"
            INSERT INTO accounts (id, tenant_id, bu_id, name, normalized_name, created_at, updated_at)
            VALUES (gen_random_uuid(), '{tenantId}', '{buId}', '{name.Replace("'", "''")}',
                    '{normalizedNameValue.Replace("'", "''")}', now(), now())";
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<long> CountAccountsAsync(Guid tenantId, Guid buId, bool useAdmin)
    {
        var connStr = useAdmin ? _fixture.ConnectionString : _fixture.AppConnectionString;
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT COUNT(*) FROM accounts
            WHERE tenant_id = '{tenantId}' AND bu_id = '{buId}'";
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<long> CountAllAccountsAsync()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM accounts";
        return (long)(await cmd.ExecuteScalarAsync())!;
    }
}

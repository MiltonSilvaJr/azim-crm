using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.Services;
using AccountManagement.Domain.Accounts.ValueObjects;
using AccountManagement.Infrastructure.Repositories;
using AccountManagement.Infrastructure.Tests.Persistence;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AccountManagement.Infrastructure.Tests.Repository;

/// <summary>
/// Testes de isolamento por Business Unit (ADR-0009, VAL-ACC-03).
///
/// Cobre:
/// - BU-ISO-01: membro com 1 BU vê só contas dessa BU.
/// - BU-ISO-02: admin tenant-wide vê todas as BUs do tenant.
/// - BU-ISO-03: membro com grant cross-BU vê o conjunto de BUs autorizadas.
/// - BU-ISO-04: escopo vazio + não-tenant-wide ⇒ nenhuma conta (fail-closed).
/// - BU-ISO-05: nunca vaza entre tenants (ADR-0001 preservado).
/// - PBT-BU: anti-cross-BU property-based test (bu_ids e escopos arbitrários).
///
/// Mapeia: ADR-0009, VAL-ACC-03, design §13.
/// </summary>
[Collection("PostgresFixture")]
public sealed class BuScopeIsolationTests
{
    private readonly PostgresFixture _fixture;
    private readonly NameNormalizer _normalizer = new();

    public BuScopeIsolationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // =========================================================================
    // BU-ISO-01: membro com 1 BU vê só contas dessa BU
    // =========================================================================

    [Fact(DisplayName = "BU-ISO-01: membro com 1 BU vê apenas contas da própria BU")]
    public async Task SingleBuMember_SeesOnlyOwnBuAccounts()
    {
        var tenant = Guid.NewGuid();
        var bu1 = Guid.NewGuid();
        var bu2 = Guid.NewGuid();

        var accountBu1 = CreateAccount(tenant, bu1, $"Empresa BU1 {bu1:N}");
        var accountBu2 = CreateAccount(tenant, bu2, $"Empresa BU2 {bu2:N}");
        await SaveDirectly(accountBu1, accountBu2);

        // Contexto com escopo de apenas bu1
        await using var ctx = _fixture.CreateDbContextWithBuScope(tenant, new[] { bu1 });
        var accounts = await ctx.Accounts.IgnoreQueryFilters()
            .Where(a => a.TenantId == tenant)
            .ToListAsync();

        // Via filtro EF: só bu1
        await using var filteredCtx = _fixture.CreateDbContextWithBuScope(tenant, new[] { bu1 });
        var filtered = await filteredCtx.Accounts.ToListAsync();

        filtered.Should().OnlyContain(a => a.BuId == bu1,
            "membro de BU1 não deve ver contas de BU2 (ADR-0009)");
        filtered.Should().NotContain(a => a.BuId == bu2);
    }

    // =========================================================================
    // BU-ISO-02: admin tenant-wide vê todas as BUs do tenant
    // =========================================================================

    [Fact(DisplayName = "BU-ISO-02: admin tenant-wide vê contas de todas as BUs do tenant")]
    public async Task TenantWideAdmin_SeesAllBuAccountsOfTenant()
    {
        var tenant = Guid.NewGuid();
        var bu1 = Guid.NewGuid();
        var bu2 = Guid.NewGuid();
        var bu3 = Guid.NewGuid();

        var acc1 = CreateAccount(tenant, bu1, $"Emp BU1 {bu1:N}");
        var acc2 = CreateAccount(tenant, bu2, $"Emp BU2 {bu2:N}");
        var acc3 = CreateAccount(tenant, bu3, $"Emp BU3 {bu3:N}");
        await SaveDirectly(acc1, acc2, acc3);

        // Tenant-wide: passa conjunto vazio de BUs com isTenantWide=true
        await using var ctx = _fixture.CreateDbContextWithBuScope(
            tenant, Array.Empty<Guid>(), isTenantWide: true);

        var accounts = await ctx.Accounts
            .Where(a => a.TenantId == tenant)
            .ToListAsync();

        accounts.Should().HaveCount(3,
            "admin tenant-wide deve ver contas de todas as BUs do tenant");
        accounts.Should().Contain(a => a.BuId == bu1);
        accounts.Should().Contain(a => a.BuId == bu2);
        accounts.Should().Contain(a => a.BuId == bu3);
    }

    // =========================================================================
    // BU-ISO-03: membro com grant cross-BU vê o conjunto autorizado
    // =========================================================================

    [Fact(DisplayName = "BU-ISO-03: membro com grant cross-BU vê contas do conjunto de BUs autorizadas")]
    public async Task CrossBuMember_SeesAccountsOfGrantedBus()
    {
        var tenant = Guid.NewGuid();
        var bu1 = Guid.NewGuid();
        var bu2 = Guid.NewGuid();
        var bu3 = Guid.NewGuid();

        var acc1 = CreateAccount(tenant, bu1, $"Emp BU1 {bu1:N}");
        var acc2 = CreateAccount(tenant, bu2, $"Emp BU2 {bu2:N}");
        var acc3 = CreateAccount(tenant, bu3, $"Emp BU3 {bu3:N}");
        await SaveDirectly(acc1, acc2, acc3);

        // Escopo: bu1 + bu2 (grant cross-BU, sem bu3)
        await using var ctx = _fixture.CreateDbContextWithBuScope(
            tenant, new[] { bu1, bu2 });

        var accounts = await ctx.Accounts
            .Where(a => a.TenantId == tenant)
            .ToListAsync();

        accounts.Should().HaveCount(2,
            "membro com acesso a BU1+BU2 não deve ver contas de BU3");
        accounts.Should().Contain(a => a.BuId == bu1);
        accounts.Should().Contain(a => a.BuId == bu2);
        accounts.Should().NotContain(a => a.BuId == bu3);
    }

    // =========================================================================
    // BU-ISO-04: escopo vazio + não-tenant-wide ⇒ fail-closed (zero contas)
    // =========================================================================

    [Fact(DisplayName = "BU-ISO-04: escopo de BU vazio e não-tenant-wide retorna zero contas (fail-closed)")]
    public async Task EmptyBuScope_NotTenantWide_ReturnsNoAccounts()
    {
        var tenant = Guid.NewGuid();
        var bu1 = Guid.NewGuid();

        var acc = CreateAccount(tenant, bu1, $"Empresa Fail-Closed {bu1:N}");
        await SaveDirectly(acc);

        // Escopo vazio + não-tenant-wide: fail-closed
        await using var ctx = _fixture.CreateDbContextWithBuScope(
            tenant, Array.Empty<Guid>(), isTenantWide: false);

        var accounts = await ctx.Accounts
            .Where(a => a.TenantId == tenant)
            .ToListAsync();

        accounts.Should().BeEmpty(
            "escopo de BU vazio sem tenant-wide deve retornar zero contas (fail-closed — ADR-0009)");
    }

    // =========================================================================
    // BU-ISO-05: nunca vaza entre tenants (ADR-0001 preservado)
    // =========================================================================

    [Fact(DisplayName = "BU-ISO-05: filtro de BU não vaza contas de outro tenant mesmo com tenant-wide")]
    public async Task BuFilter_NeverLeaksCrossTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var bu = Guid.NewGuid(); // mesmo bu_id nos dois tenants (edge case)

        var accA = CreateAccount(tenantA, bu, $"Emp TenantA {tenantA:N}");
        var accB = CreateAccount(tenantB, bu, $"Emp TenantB {tenantB:N}");
        await SaveDirectly(accA, accB);

        // TenantA com tenant-wide: deve ver apenas contas do tenantA
        await using var ctx = _fixture.CreateDbContextWithBuScope(
            tenantA, Array.Empty<Guid>(), isTenantWide: true);

        var accounts = await ctx.Accounts.ToListAsync();

        accounts.Should().OnlyContain(a => a.TenantId == tenantA,
            "filtro de tenant nunca é relaxado, mesmo com tenant-wide de BU (ADR-0001)");
        accounts.Should().NotContain(a => a.TenantId == tenantB);
    }

    // =========================================================================
    // PBT-BU: property-based test anti-cross-BU (ADR-0009)
    // =========================================================================

    /// <summary>
    /// Para qualquer combinação arbitrária de bu_ids e escopos, o filtro EF Core
    /// nunca retorna contas fora do escopo autorizado.
    ///
    /// Mínimo de 50 combinações geradas.
    /// </summary>
    [Property(MaxTest = 50, QuietOnSuccess = true)]
    public Property PBT_BU_EfFilter_NeverReturnsCrossScope()
    {
        var genGuid = ArbMap.Default.ArbFor<Guid>().Generator;
        // Gera (tenantId, buA, buB distintos de buA)
        var gen = genGuid.SelectMany(tenant =>
            genGuid.SelectMany(buA =>
                genGuid.Where(buB => buB != buA)
                    .Select(buB => (tenant, buA, buB))));

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (tenant, buA, buB) = tuple;
            return RunPbtBuAsync(tenant, buA, buB).GetAwaiter().GetResult();
        });
    }

    private async Task<bool> RunPbtBuAsync(Guid tenant, Guid buA, Guid buB)
    {
        var accA = CreateAccount(tenant, buA, $"PBT-BU-A-{buA:N}");
        var accB = CreateAccount(tenant, buB, $"PBT-BU-B-{buB:N}");
        await SaveDirectly(accA, accB);

        // Escopo: apenas buA
        await using var ctx = _fixture.CreateDbContextWithBuScope(tenant, new[] { buA });
        var accounts = await ctx.Accounts
            .Where(a => a.TenantId == tenant)
            .ToListAsync();

        // Nunca deve retornar conta de buB
        return !accounts.Any(a => a.BuId == buB);
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

    private async Task SaveDirectly(params Account[] accounts)
    {
        await using var ctx = _fixture.CreateDbContextNoFilter();
        ctx.Accounts.AddRange(accounts);
        await ctx.SaveChangesAsync();
    }
}

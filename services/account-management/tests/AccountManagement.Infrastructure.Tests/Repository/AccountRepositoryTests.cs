using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.Repositories;
using AccountManagement.Domain.Accounts.Services;
using AccountManagement.Domain.Accounts.ValueObjects;
using AccountManagement.Infrastructure.Repositories;
using AccountManagement.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AccountManagement.Infrastructure.Tests.Repository;

/// <summary>
/// Testes de integração para <see cref="AccountRepository"/> com PostgreSQL real.
///
/// Cobre TASK-10 (ST-01..ST-05):
/// - GetByIdAsync: retorna null para account de outro tenant (ST-01).
/// - SearchAsync: não retorna contas de outro tenant (ST-01).
/// - SearchSimilarAsync: opera exclusivamente no tenant do contexto (ST-01).
/// - PBT-04 (ST-02): para qualquer combinação de tenants, nenhuma operação retorna
///   dados de outro tenant — gate CI obrigatório (RNF 5.2).
/// - Filtro global: teste de regressão que valida filtro ativo via SQL direto (ST-04).
///
/// Mapeia: TASK-10, RNF 5.1..5.3, PBT-04, DD-002.
/// </summary>
[Collection("PostgresFixture")]
public sealed class AccountRepositoryTests
{
    private readonly PostgresFixture _fixture;
    private readonly NameNormalizer _normalizer = new();

    public AccountRepositoryTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // =========================================================================
    // ST-01 — Testes de isolamento de tenant
    // =========================================================================

    [Fact]
    public async Task GetByIdAsync_returns_null_for_account_of_other_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // Setup: salvar conta no tenantB
        var accountB = CreateTestAccount(tenantB, "Empresa B");
        await SaveAccountDirectly(accountB);

        // Query com contexto de tenantA: deve retornar null para id da conta de tenantB
        await using var ctx = _fixture.CreateDbContext(tenantA);
        var repo = new AccountRepository(ctx);

        var result = await repo.GetByIdAsync(accountB.Id);

        result.Should().BeNull("o filtro global impede acesso cross-tenant (DD-002)");
    }

    [Fact]
    public async Task SearchAsync_does_not_return_accounts_of_other_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var accountA = CreateTestAccount(tenantA, $"Acme Corp A {tenantA:N}");
        var accountB = CreateTestAccount(tenantB, $"Acme Corp B {tenantB:N}");
        await SaveAccountDirectly(accountA);
        await SaveAccountDirectly(accountB);

        await using var ctx = _fixture.CreateDbContext(tenantA);
        var repo = new AccountRepository(ctx);

        var results = await repo.SearchAsync("Acme", 1, 50);

        results.Should().OnlyContain(a => a.TenantId == tenantA,
            "SearchAsync opera exclusivamente dentro do tenant autenticado (Req 10.2)");
        results.Should().NotContain(a => a.TenantId == tenantB);
    }

    [Fact]
    public async Task SearchSimilarAsync_operates_exclusively_in_current_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // Mesma forma normalizada, tenants diferentes
        var nameA = $"PagAI-{tenantA:N}";
        var accountA = CreateTestAccount(tenantA, nameA);
        var accountB = CreateTestAccount(tenantB, nameA); // mesmo nome normalizado
        await SaveAccountDirectly(accountA);
        await SaveAccountDirectly(accountB);

        var normalizedName = _normalizer.NormalizeName(nameA);

        await using var ctx = _fixture.CreateDbContext(tenantA);
        var repo = new AccountRepository(ctx);

        var results = await repo.SearchSimilarAsync(normalizedName);

        results.Should().OnlyContain(a => a.TenantId == tenantA,
            "SearchSimilarAsync opera exclusivamente no tenant do contexto (DD-006)");
        results.Should().NotContain(a => a.TenantId == tenantB);
    }

    [Fact]
    public async Task SaveAsync_persists_account_and_loads_back()
    {
        var tenantId = Guid.NewGuid();
        var account = CreateTestAccount(tenantId, "Empresa Persistência");

        await using var saveCtx = _fixture.CreateDbContext(tenantId);
        var saveRepo = new AccountRepository(saveCtx);
        await saveRepo.SaveAsync(account);

        // Carrega em novo contexto para evitar cache
        await using var loadCtx = _fixture.CreateDbContext(tenantId);
        var loadRepo = new AccountRepository(loadCtx);
        var loaded = await loadRepo.GetByIdAsync(account.Id);

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(account.Id);
        loaded.TenantId.Should().Be(account.TenantId);
        loaded.Name.Value.Should().Be(account.Name.Value);
    }

    // =========================================================================
    // ST-04 — Regressão: filtro global ativo confirmado por contagem SQL
    // =========================================================================

    [Fact]
    public async Task Global_filter_is_active_and_prevents_cross_tenant_read()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var accountA = CreateTestAccount(tenantA, $"FilterTest A {tenantA:N}");
        var accountB = CreateTestAccount(tenantB, $"FilterTest B {tenantB:N}");
        await SaveAccountDirectly(accountA);
        await SaveAccountDirectly(accountB);

        // Conta total via SQL direto: deve ter AMBAS as contas no banco
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using var totalCmd = conn.CreateCommand();
        totalCmd.CommandText = $@"
            SELECT COUNT(*) FROM accounts
            WHERE id = '{accountA.Id}' OR id = '{accountB.Id}'";
        var total = (long)(await totalCmd.ExecuteScalarAsync())!;
        total.Should().Be(2L, "ambas as contas existem no banco");

        // Conta via EF Core com filtro: só tenantA
        await using var ctx = _fixture.CreateDbContext(tenantA);
        var filtered = await ctx.Accounts
            .Where(a => a.Id == accountA.Id || a.Id == accountB.Id)
            .ToListAsync();
        filtered.Should().HaveCount(1, "o filtro global retorna apenas dados do tenantA");
        filtered.Should().OnlyContain(a => a.TenantId == tenantA);
    }

    // =========================================================================
    // ST-02 — PBT-04: anti-cross-tenant gate CI obrigatório (RNF 5.2)
    // =========================================================================

    /// <summary>
    /// PBT-04: para qualquer par de tenants distintos gerado arbitrariamente,
    /// nenhuma operação de repositório no contexto de um tenant retorna dados de outro tenant.
    ///
    /// Mínimo de 100 combinações geradas (design §13, TASK-10 ST-02).
    /// Gate CI obrigatório (RNF 5.2).
    /// </summary>
    [Property(MaxTest = 100, QuietOnSuccess = true)]
    public Property PBT04_repository_never_crosses_tenant_boundary()
    {
        // Gerador de dois GUIDs distintos representando dois tenants diferentes
        // Usa API FsCheck.Fluent (ArbMap + SelectMany) compatível com FsCheck 3.x
        var genGuid = ArbMap.Default.ArbFor<Guid>().Generator;
        var gen = genGuid.SelectMany(
            t1 => genGuid.Where(t2 => t2 != t1).Select(t2 => (t1, t2)));

        return Prop.ForAll(gen.ToArbitrary(), (ValueTuple<Guid, Guid> pair) =>
        {
            var (tenantA, tenantB) = pair;
            return RunPbt04Async(tenantA, tenantB).GetAwaiter().GetResult();
        });
    }

    private async Task<bool> RunPbt04Async(Guid tenantA, Guid tenantB)
    {
        // Criar uma conta em cada tenant com nome único
        var nameA = $"PBT04-A-{tenantA:N}";
        var nameB = $"PBT04-B-{tenantB:N}";
        var accountA = CreateTestAccount(tenantA, nameA);
        var accountB = CreateTestAccount(tenantB, nameB);
        await SaveAccountDirectly(accountA);
        await SaveAccountDirectly(accountB);

        // Contexto de tenantA: nenhuma operação deve retornar dados de tenantB
        await using var ctx = _fixture.CreateDbContext(tenantA);
        var repo = new AccountRepository(ctx);

        // GetByIdAsync com id de conta de outro tenant
        var byId = await repo.GetByIdAsync(accountB.Id);
        if (byId is not null) return false;

        // SearchAsync não deve retornar contas de tenantB
        var searched = await repo.SearchAsync(nameB, 1, 50);
        if (searched.Any(a => a.TenantId == tenantB)) return false;

        // SearchSimilarAsync com nome normalizado de conta de tenantB
        var normalizedNameB = _normalizer.NormalizeName(nameB);
        var similar = await repo.SearchSimilarAsync(normalizedNameB);
        if (similar.Any(a => a.TenantId == tenantB)) return false;

        return true;
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private Account CreateTestAccount(Guid tenantId, string name)
    {
        return Account.Create(
            tenantId: tenantId,
            name: AccountName.Create(name),
            website: null,
            notes: null,
            normalizer: _normalizer);
    }

    private async Task SaveAccountDirectly(Account account)
    {
        // Insere diretamente via SQL para evitar o filtro global no setup
        await using var ctx = _fixture.CreateDbContextNoFilter();
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();
    }
}

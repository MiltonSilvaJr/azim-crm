using AccountManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AccountManagement.Infrastructure.Tests.Persistence;

/// <summary>
/// Testes de integração (ST-01 TASK-08) verificando schema das tabelas
/// <c>accounts</c> e <c>contacts</c> em PostgreSQL real via Testcontainers.
///
/// Valida:
/// - Tabelas existem com colunas e constraints esperadas (design §7).
/// - Índice <c>idx_accounts_tenant_normalized_name</c> existe e é não-unique (DD-006).
/// - Índice <c>idx_contacts_tenant_account</c> existe.
/// - Constraints de check ativas.
///
/// Mapeia: TASK-08 (ST-01), design §7, DD-006.
/// </summary>
[Collection("PostgresFixture")]
public sealed class DbContextSchemaTests
{
    private readonly PostgresFixture _fixture;

    public DbContextSchemaTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Accounts_table_exists_with_expected_columns()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT column_name
            FROM information_schema.columns
            WHERE table_name = 'accounts'
            ORDER BY column_name";

        var columns = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(0));

        Assert.Contains("id", columns);
        Assert.Contains("tenant_id", columns);
        Assert.Contains("name", columns);
        Assert.Contains("normalized_name", columns);
        Assert.Contains("website", columns);
        Assert.Contains("notes", columns);
        Assert.Contains("created_at", columns);
        Assert.Contains("updated_at", columns);
        Assert.DoesNotContain("bu_id", columns); // Req 2.3 — sem bu_id
    }

    [Fact]
    public async Task Contacts_table_exists_with_expected_columns()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT column_name
            FROM information_schema.columns
            WHERE table_name = 'contacts'
            ORDER BY column_name";

        var columns = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(0));

        Assert.Contains("id", columns);
        Assert.Contains("tenant_id", columns);
        Assert.Contains("account_id", columns);
        Assert.Contains("name", columns);
        Assert.Contains("email", columns);
        Assert.Contains("phone", columns);
        Assert.Contains("role", columns);
        Assert.Contains("privacy_state", columns);
        Assert.Contains("forgotten_at", columns);
        Assert.Contains("forgotten_by", columns);
        Assert.Contains("created_at", columns);
        Assert.Contains("updated_at", columns);
        Assert.DoesNotContain("bu_id", columns); // Req 2.3 — sem bu_id
    }

    [Fact]
    public async Task Index_idx_accounts_tenant_normalized_name_exists_and_is_not_unique()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT ix.indisunique
            FROM pg_indexes pi
            JOIN pg_class c ON c.relname = pi.indexname
            JOIN pg_index ix ON ix.indexrelid = c.oid
            WHERE pi.tablename = 'accounts'
              AND pi.indexname = 'idx_accounts_tenant_normalized_name'";

        var result = await cmd.ExecuteScalarAsync();
        Assert.NotNull(result);
        Assert.Equal(false, result); // NÃO unique (DD-006)
    }

    [Fact]
    public async Task Index_idx_contacts_tenant_account_exists()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM pg_indexes
            WHERE tablename = 'contacts'
              AND indexname = 'idx_contacts_tenant_account'";

        var count = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(1L, count);
    }

    [Fact]
    public async Task Check_constraint_chk_accounts_name_not_blank_is_active()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM information_schema.check_constraints
            WHERE constraint_name = 'chk_accounts_name_not_blank'";

        var count = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(1L, count);
    }

    [Fact]
    public async Task Check_constraint_chk_contacts_privacy_state_is_active()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM information_schema.check_constraints
            WHERE constraint_name = 'chk_contacts_privacy_state'";

        var count = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(1L, count);
    }

    [Fact]
    public async Task Global_tenant_filter_prevents_cross_tenant_access()
    {
        // ST-02 TASK-08: inserir registros de dois tenants; query via DbContext com tenant A
        // não retorna registros de tenant B.

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // Setup: inserir contas via SQL direto para contornar o filtro global
        await using var setupConn = new NpgsqlConnection(_fixture.ConnectionString);
        await setupConn.OpenAsync();

        await using var insertB = setupConn.CreateCommand();
        insertB.CommandText = $@"
            INSERT INTO accounts (id, tenant_id, name, normalized_name, created_at, updated_at)
            VALUES ('{Guid.NewGuid()}', '{tenantB}', 'Empresa B', 'empresa b', now(), now())";
        await insertB.ExecuteNonQueryAsync();

        await using var insertA = setupConn.CreateCommand();
        insertA.CommandText = $@"
            INSERT INTO accounts (id, tenant_id, name, normalized_name, created_at, updated_at)
            VALUES ('{Guid.NewGuid()}', '{tenantA}', 'Empresa A', 'empresa a', now(), now())";
        await insertA.ExecuteNonQueryAsync();

        // Query com contexto de tenantA: não deve retornar Empresa B
        await using var ctxA = _fixture.CreateDbContext(tenantA);
        var accounts = await ctxA.Accounts.ToListAsync();

        Assert.All(accounts, a => Assert.Equal(tenantA, a.TenantId));
        Assert.DoesNotContain(accounts, a => a.TenantId == tenantB);
    }
}

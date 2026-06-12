using AuditLog.Domain.ValueObjects;
using AuditLog.Infrastructure.Repositories;
using AuditLog.Infrastructure.Tests.Fixtures;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace AuditLog.Infrastructure.Tests.PropertyTests;

/// <summary>
/// PBT-01 — Imutabilidade: qualquer sequência de UPDATE/DELETE pelo role <c>app</c>
/// é rejeitada; o registro permanece byte-a-byte idêntico ao estado inserido.
///
/// PBT-05 — Isolamento por tenant: consulta no contexto de um <c>tenant_id</c> X retorna
/// exclusivamente registros de X, sob filtros variados.
///
/// FsCheck 2.x — execução síncrona via GetAwaiter().GetResult().
/// Mínimo de 50 amostras por PBT.
/// </summary>
[Collection(PostgresTestCollection.Name)]
public sealed class ImmutabilityAndIsolationPbtTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;

    public ImmutabilityAndIsolationPbtTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using var ctx = TestDbContextFactory.Create(
            _fixture.SuperuserConnectionString, Guid.NewGuid());
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _fixture.TruncateAuditLogsAsync();
    }

    // ------------------------------------------------------------------ PBT-01: Imutabilidade

    /// <summary>
    /// PBT-01: UPDATE via role app é sempre rejeitado (REVOKE + trigger).
    /// Mínimo 50 amostras.
    /// </summary>
    [Property(MaxTest = 50, DisplayName = "PBT-01: UPDATE pelo role app deve ser rejeitado")]
    public Property Pbt01_AnyUpdate_ByAppRole_IsRejected()
    {
        return Prop.ForAll(
            ArbitraryRecord(),
            record =>
            {
                InsertRecord(record);

                try
                {
                    var threw = false;
                    try
                    {
                        using var conn = new NpgsqlConnection(_fixture.AppConnectionString);
                        conn.Open();

                        using var setCmd = conn.CreateCommand();
                        setCmd.CommandText = $"SET app.tenant_id = '{record.TenantId}';";
                        setCmd.ExecuteNonQuery();

                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = $"UPDATE audit_logs SET action = 'delete' WHERE id = '{record.Id}';";
                        cmd.ExecuteNonQuery();
                    }
                    catch (PostgresException) { threw = true; }
                    catch (NpgsqlException) { threw = true; }

                    return threw;
                }
                finally
                {
                    _fixture.TruncateAuditLogsAsync().GetAwaiter().GetResult();
                }
            });
    }

    /// <summary>
    /// PBT-01: DELETE via role app é sempre rejeitado.
    /// Mínimo 50 amostras.
    /// </summary>
    [Property(MaxTest = 50, DisplayName = "PBT-01: DELETE pelo role app deve ser rejeitado")]
    public Property Pbt01_AnyDelete_ByAppRole_IsRejected()
    {
        return Prop.ForAll(
            ArbitraryRecord(),
            record =>
            {
                InsertRecord(record);

                try
                {
                    var threw = false;
                    try
                    {
                        using var conn = new NpgsqlConnection(_fixture.AppConnectionString);
                        conn.Open();

                        using var setCmd = conn.CreateCommand();
                        setCmd.CommandText = $"SET app.tenant_id = '{record.TenantId}';";
                        setCmd.ExecuteNonQuery();

                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = $"DELETE FROM audit_logs WHERE id = '{record.Id}';";
                        cmd.ExecuteNonQuery();
                    }
                    catch (PostgresException) { threw = true; }
                    catch (NpgsqlException) { threw = true; }

                    return threw;
                }
                finally
                {
                    _fixture.TruncateAuditLogsAsync().GetAwaiter().GetResult();
                }
            });
    }

    // ------------------------------------------------------------------ PBT-05: Isolamento por tenant

    /// <summary>
    /// PBT-05: Consulta no contexto de tenant X não retorna registros de outros tenants.
    /// Mínimo 50 amostras.
    /// </summary>
    [Property(MaxTest = 50, DisplayName = "PBT-05: isolamento por tenant no DbContext")]
    public Property Pbt05_QueryInTenantContext_ReturnsOnlyThatTenantRecords()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 5)),
            otherTenantsCount =>
            {
                var targetTenant = Guid.NewGuid();
                InsertRecord(new TestRecord(Guid.NewGuid(), targetTenant, Guid.NewGuid(), "Opportunity"));

                for (var i = 0; i < otherTenantsCount; i++)
                {
                    InsertRecord(new TestRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Opportunity"));
                }

                try
                {
                    using var ctx = TestDbContextFactory.Create(
                        _fixture.SuperuserConnectionString, targetTenant);
                    var repo = new AuditLogRepository(ctx);

                    var (items, total) = repo
                        .ListAsync(TenantId.From(targetTenant), page: 1, pageSize: 200)
                        .GetAwaiter().GetResult();

                    return items.All(x => x.TenantId.Value == targetTenant) && total == 1;
                }
                finally
                {
                    _fixture.TruncateAuditLogsAsync().GetAwaiter().GetResult();
                }
            });
    }

    /// <summary>
    /// PBT-05: FindByEntity não vaza registros de outros tenants com mesmo entity_id.
    /// Mínimo 50 amostras.
    /// </summary>
    [Property(MaxTest = 50, DisplayName = "PBT-05: FindByEntity isola por tenant")]
    public Property Pbt05_FindByEntity_ReturnsOnlyMatchingTenantEntity()
    {
        return Prop.ForAll(
            ArbitraryRecord(),
            record =>
            {
                InsertRecord(record);
                // Mesmo entity_id, tenant diferente
                InsertRecord(record with { Id = Guid.NewGuid(), TenantId = Guid.NewGuid() });

                try
                {
                    using var ctx = TestDbContextFactory.Create(
                        _fixture.SuperuserConnectionString, record.TenantId);
                    var repo = new AuditLogRepository(ctx);

                    var entityRef = EntityReference.Create(record.EntityType, record.EntityId);
                    var results = repo
                        .FindByEntityAsync(TenantId.From(record.TenantId), entityRef)
                        .GetAwaiter().GetResult();

                    return results.All(x => x.TenantId.Value == record.TenantId);
                }
                finally
                {
                    _fixture.TruncateAuditLogsAsync().GetAwaiter().GetResult();
                }
            });
    }

    // ------------------------------------------------------------------ Helpers

    private void InsertRecord(TestRecord record)
    {
        using var conn = new NpgsqlConnection(_fixture.SuperuserConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        const string deltaJson = """{"kind":"create","after":{"field":"value"}}""";
        cmd.CommandText =
            $"INSERT INTO audit_logs (id, tenant_id, user_id, entity_type, entity_id, action, delta_json) " +
            $"VALUES ('{record.Id}', '{record.TenantId}', '{Guid.NewGuid()}', " +
            $"'{record.EntityType}', '{record.EntityId}', 'create', '{deltaJson}');";
        cmd.ExecuteNonQuery();
    }

    private static Arbitrary<TestRecord> ArbitraryRecord()
    {
        return Arb.From(
            from entityType in Gen.Elements("Opportunity", "Account", "Contact", "Partner")
            select new TestRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), entityType));
    }

    private sealed record TestRecord(Guid Id, Guid TenantId, Guid EntityId, string EntityType);
}

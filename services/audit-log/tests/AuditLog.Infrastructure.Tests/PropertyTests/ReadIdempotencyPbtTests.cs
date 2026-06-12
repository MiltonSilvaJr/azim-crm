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
/// PBT-06 — Idempotência de leitura: N execuções de qualquer consulta não alteram
/// conjunto, ordem nem conteúdo persistido.
/// Mínimo 100 amostras (design §13.1).
/// FsCheck 2.x — execução síncrona via GetAwaiter().GetResult().
/// </summary>
[Collection(PostgresTestCollection.Name)]
public sealed class ReadIdempotencyPbtTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;

    public ReadIdempotencyPbtTests(PostgresContainerFixture fixture)
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

    /// <summary>
    /// PBT-06: ListAsync N vezes com os mesmos filtros retorna sempre o mesmo resultado.
    /// Mínimo 100 amostras.
    /// </summary>
    [Property(MaxTest = 100, DisplayName = "PBT-06: ListAsync é idempotente")]
    public Property Pbt06_ListAsync_IsIdempotent_DoesNotAlterPersistedData()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 5)),
            Arb.From(Gen.Choose(2, 4)),
            (recordCount, queryExecutions) =>
            {
                var tenantId = Guid.NewGuid();

                try
                {
                    for (var i = 0; i < recordCount; i++)
                    {
                        InsertRecord(tenantId, "Entity");
                    }

                    var allResults = new List<Guid[]>();
                    for (var run = 0; run < queryExecutions; run++)
                    {
                        using var ctx = TestDbContextFactory.Create(
                            _fixture.SuperuserConnectionString, tenantId);
                        var repo = new AuditLogRepository(ctx);

                        var (items, _) = repo
                            .ListAsync(TenantId.From(tenantId), page: 1, pageSize: 200)
                            .GetAwaiter().GetResult();

                        allResults.Add(items.Select(x => x.Id.Value).ToArray());
                    }

                    // Todos os runs retornam o mesmo conjunto na mesma ordem
                    var first = allResults[0];
                    for (var i = 1; i < allResults.Count; i++)
                    {
                        if (!first.SequenceEqual(allResults[i]))
                            return false;
                    }

                    return CountRecords(tenantId) == recordCount;
                }
                finally
                {
                    _fixture.TruncateAuditLogsAsync().GetAwaiter().GetResult();
                }
            });
    }

    /// <summary>
    /// PBT-06: FindByEntityAsync N vezes retorna sempre o mesmo resultado.
    /// Mínimo 100 amostras.
    /// </summary>
    [Property(MaxTest = 100, DisplayName = "PBT-06: FindByEntityAsync é idempotente")]
    public Property Pbt06_FindByEntity_IsIdempotent()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(2, 4)),
            queryExecutions =>
            {
                var tenantId = Guid.NewGuid();
                var entityId = Guid.NewGuid();

                try
                {
                    InsertRecord(tenantId, "Opportunity", entityId);
                    InsertRecord(tenantId, "Opportunity", entityId);

                    var entityRef = EntityReference.Create("Opportunity", entityId);
                    var counts = new List<int>();

                    for (var run = 0; run < queryExecutions; run++)
                    {
                        using var ctx = TestDbContextFactory.Create(
                            _fixture.SuperuserConnectionString, tenantId);
                        var repo = new AuditLogRepository(ctx);

                        var items = repo
                            .FindByEntityAsync(TenantId.From(tenantId), entityRef)
                            .GetAwaiter().GetResult();

                        counts.Add(items.Count);
                    }

                    return counts.All(c => c == 2);
                }
                finally
                {
                    _fixture.TruncateAuditLogsAsync().GetAwaiter().GetResult();
                }
            });
    }

    // ------------------------------------------------------------------ Helpers

    private void InsertRecord(Guid tenantId, string entityType, Guid? entityId = null)
    {
        var id = Guid.NewGuid();
        var eid = entityId ?? Guid.NewGuid();
        var actorId = Guid.NewGuid();
        const string deltaJson = """{"kind":"create","after":{"field":"value"}}""";

        using var conn = new NpgsqlConnection(_fixture.SuperuserConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"INSERT INTO audit_logs (id, tenant_id, user_id, entity_type, entity_id, action, delta_json) " +
            $"VALUES ('{id}', '{tenantId}', '{actorId}', '{entityType}', '{eid}', 'create', '{deltaJson}');";
        cmd.ExecuteNonQuery();
    }

    private int CountRecords(Guid tenantId)
    {
        using var conn = new NpgsqlConnection(_fixture.SuperuserConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM audit_logs WHERE tenant_id = '{tenantId}';";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}

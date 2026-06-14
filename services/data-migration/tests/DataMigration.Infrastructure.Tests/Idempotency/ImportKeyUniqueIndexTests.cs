using DataMigration.Infrastructure.Idempotency;
using DataMigration.Infrastructure.Persistence;
using DataMigration.Infrastructure.Tests.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace DataMigration.Infrastructure.Tests.Idempotency;

/// <summary>
/// Testes de integração que validam o índice único <c>uq_migration_log_import_key</c>
/// (DD-003) com PostgreSQL real via Testcontainers.
///
/// Valida que a constraint de banco rejeita inserção de import_key duplicada
/// mesmo que o código de aplicação tente inserir.
///
/// Rastreia: TASK-19, DD-003, design §7.
/// </summary>
[Collection("PostgresContainer")]
public sealed class ImportKeyUniqueIndexTests
{
    private readonly PostgresContainerFixture _fixture;

    public ImportKeyUniqueIndexTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ImportKey_Duplicada_RejeitionaPeloBanco()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var noPoolConnStr = _fixture.ConnectionString + ";Pooling=false";

        // Cria um job no tenant.
        Guid jobId;
        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            var job = DataMigration.Domain.Aggregates.MigrationJob.Create(
                tenantId: tenantId,
                sourceFileName: "idem-test.xlsx",
                sourceFileSizeBytes: 256,
                sourceFileHash: Guid.NewGuid().ToString("N"),
                detectedRowCount: 2,
                createdBy: Guid.NewGuid());

            await ctx.MigrationJobs.AddAsync(job);
            await ctx.SaveChangesAsync();
            jobId = job.Id;
        }

        // Calcula a import_key determinística.
        var importKey = ImportKeyCalculator.Calculate(
            tenantId, "Pipeline", 5, "EmpresaXPTO|AZ-0095|100000");

        // Primeira inserção de log com import_key.
        await using var conn = new NpgsqlConnection(noPoolConnStr);
        await conn.OpenAsync();
        await using var setCmd = conn.CreateCommand();
        setCmd.CommandText = $"SET app.current_tenant = '{tenantId}'";
        await setCmd.ExecuteNonQueryAsync();

        await using var insertCmd1 = conn.CreateCommand();
        insertCmd1.CommandText = """
            INSERT INTO migration_logs (id, tenant_id, migration_job_id, source_sheet, source_row_index, status, message, import_key)
            VALUES (gen_random_uuid(), @tid, @jid, 'Pipeline', 5, 'ok', 'processado', @key)
            """;
        insertCmd1.Parameters.AddWithValue("tid", tenantId);
        insertCmd1.Parameters.AddWithValue("jid", jobId);
        insertCmd1.Parameters.AddWithValue("key", importKey);
        await insertCmd1.ExecuteNonQueryAsync();

        // Act — segunda inserção com MESMA import_key deve falhar (uq_migration_log_import_key).
        await using var insertCmd2 = conn.CreateCommand();
        insertCmd2.CommandText = """
            INSERT INTO migration_logs (id, tenant_id, migration_job_id, source_sheet, source_row_index, status, message, import_key)
            VALUES (gen_random_uuid(), @tid, @jid, 'Pipeline', 5, 'ok', 'processado novamente', @key)
            """;
        insertCmd2.Parameters.AddWithValue("tid", tenantId);
        insertCmd2.Parameters.AddWithValue("jid", jobId);
        insertCmd2.Parameters.AddWithValue("key", importKey);

        var act = async () => await insertCmd2.ExecuteNonQueryAsync();

        // Assert — constraint única deve rejeitar.
        await act.Should().ThrowAsync<PostgresException>(
            "uq_migration_log_import_key deve rejeitar import_key duplicada (DD-003)");
    }

    [Fact]
    public async Task ImportKey_NullDuplicada_Permitida()
    {
        // Arrange — import_key NULL pode ter múltiplas entradas (índice parcial WHERE import_key IS NOT NULL).
        var tenantId = Guid.NewGuid();
        var noPoolConnStr = _fixture.ConnectionString + ";Pooling=false";

        Guid jobId;
        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            var job = DataMigration.Domain.Aggregates.MigrationJob.Create(
                tenantId: tenantId,
                sourceFileName: "null-key-test.xlsx",
                sourceFileSizeBytes: 128,
                sourceFileHash: Guid.NewGuid().ToString("N"),
                detectedRowCount: 2,
                createdBy: Guid.NewGuid());

            await ctx.MigrationJobs.AddAsync(job);
            await ctx.SaveChangesAsync();
            jobId = job.Id;
        }

        await using var conn = new NpgsqlConnection(noPoolConnStr);
        await conn.OpenAsync();
        await using var setCmd = conn.CreateCommand();
        setCmd.CommandText = $"SET app.current_tenant = '{tenantId}'";
        await setCmd.ExecuteNonQueryAsync();

        // Act — duas linhas com import_key NULL (índice parcial não cobre NULL).
        for (var i = 0; i < 2; i++)
        {
            await using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = """
                INSERT INTO migration_logs (id, tenant_id, migration_job_id, source_sheet, source_row_index, status, message)
                VALUES (gen_random_uuid(), @tid, @jid, 'Pipeline', @row, 'aviso', 'sem chave')
                """;
            insertCmd.Parameters.AddWithValue("tid", tenantId);
            insertCmd.Parameters.AddWithValue("jid", jobId);
            insertCmd.Parameters.AddWithValue("row", i);
            await insertCmd.ExecuteNonQueryAsync();
        }

        // Assert — nenhuma exceção: NULL não é coberto pelo índice único parcial.
        // (Verificação implícita: se chegou aqui, o banco aceitou ambas as inserções.)
        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM migration_logs WHERE migration_job_id = '{jobId}'";
        var count = (long)(await countCmd.ExecuteScalarAsync())!;
        count.Should().Be(2, "duas linhas com import_key NULL são permitidas pelo índice parcial");
    }
}

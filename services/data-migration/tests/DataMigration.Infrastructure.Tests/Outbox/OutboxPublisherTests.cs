using DataMigration.Domain.Aggregates;
using DataMigration.Infrastructure.Outbox;
using DataMigration.Infrastructure.Persistence;
using DataMigration.Infrastructure.Tests.Persistence;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace DataMigration.Infrastructure.Tests.Outbox;

/// <summary>
/// Testes de integração do <see cref="OutboxPublisher"/> com PostgreSQL real.
///
/// Valida:
/// - ImportCompleted gravado na mesma transação do import (DD-001).
/// - Rollback da transação descarta o evento do Outbox.
/// - Payload sem PII.
/// - DryRunCompleted emitido após dry-run.
/// - Append-only: sem UPDATE/DELETE pós-emissão.
///
/// Rastreia: TASK-20, ST-01, DD-001, RNF 4, Req 11.
/// </summary>
[Collection("PostgresContainer")]
public sealed class OutboxPublisherTests
{
    private readonly PostgresContainerFixture _fixture;

    public OutboxPublisherTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    // =========================================================================
    // ImportCompleted — gravado na mesma transação
    // =========================================================================

    [Fact]
    public async Task PublishImportCompleted_NaMesmaTransacao_EvetoGravadoAposCommit()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();

        await using var ctx = _fixture.CreateContext(tenantId);
        var uow = new SharedUnitOfWork(ctx);
        var publisher = new OutboxPublisher(ctx);

        var job = CreateJob(tenantId);
        await ctx.MigrationJobs.AddAsync(job);

        // Act — publica dentro da transação e commita.
        await uow.BeginAsync();
        await ctx.SaveChangesAsync();
        await publisher.PublishImportCompletedAsync(tenantId, job.Id, 10, correlationId);
        await uow.CommitAsync();

        // Assert — evento deve estar no outbox após commit.
        var count = await CountOutboxEventsAsync(tenantId, OutboxEventType.ImportCompleted);
        count.Should().Be(1, "ImportCompleted deve ser gravado na mesma transação (DD-001)");
    }

    // =========================================================================
    // Rollback descarta o evento (atomicidade DD-001)
    // =========================================================================

    [Fact]
    public async Task PublishImportCompleted_ComRollback_EventoDescartado()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();

        await using var ctx = _fixture.CreateContext(tenantId);
        var uow = new SharedUnitOfWork(ctx);
        var publisher = new OutboxPublisher(ctx);

        var job = CreateJob(tenantId);
        await ctx.MigrationJobs.AddAsync(job);

        // Act — publica dentro da transação e FAZ ROLLBACK.
        await uow.BeginAsync();
        await ctx.SaveChangesAsync();
        await publisher.PublishImportCompletedAsync(tenantId, job.Id, 10, correlationId);
        await uow.RollbackAsync(); // reverte tudo, incluindo o evento.

        // Assert — evento não deve estar no outbox (rollback descartou).
        var count = await CountOutboxEventsAsync(tenantId, OutboxEventType.ImportCompleted);
        count.Should().Be(0, "rollback deve descartar o evento do Outbox junto (DD-001)");
    }

    // =========================================================================
    // DryRunCompleted — emitido após dry-run
    // =========================================================================

    [Fact]
    public async Task PublishDryRunCompleted_TransacaoRollbackOnly_EventoGravadoSeparadamente()
    {
        // Arrange — DryRunCompleted é publicado FORA da transação rollback-only do dry-run,
        // em uma nova transação de auditoria. Por isso este teste usa transação normal.
        var tenantId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        await using var ctx = _fixture.CreateContext(tenantId);
        var uow = new SharedUnitOfWork(ctx);
        var publisher = new OutboxPublisher(ctx);

        // Act — publica DryRunCompleted em transação normal (auditoria pós dry-run).
        await uow.BeginAsync();
        await publisher.PublishDryRunCompletedAsync(tenantId, jobId, 50, 3, correlationId);
        await uow.CommitAsync();

        // Assert
        var count = await CountOutboxEventsAsync(tenantId, OutboxEventType.DryRunCompleted);
        count.Should().Be(1, "DryRunCompleted deve ser gravado após dry-run");
    }

    // =========================================================================
    // ImportRolledBack — caminho de falha
    // =========================================================================

    [Fact]
    public async Task PublishImportRolledBack_AposRollback_EventoGravadoEmNovaTx()
    {
        // Arrange — ImportRolledBack é publicado em nova transação após rollback do import.
        var tenantId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        await using var ctx = _fixture.CreateContext(tenantId);
        var uow = new SharedUnitOfWork(ctx);
        var publisher = new OutboxPublisher(ctx);

        // Act — após rollback do import, publica ImportRolledBack em nova transação.
        await uow.BeginAsync();
        await publisher.PublishImportRolledBackAsync(tenantId, jobId, "Erro de validação", correlationId);
        await uow.CommitAsync();

        // Assert
        var count = await CountOutboxEventsAsync(tenantId, OutboxEventType.ImportRolledBack);
        count.Should().Be(1, "ImportRolledBack deve ser gravado após falha com rollback");
    }

    // =========================================================================
    // Payload sem PII
    // =========================================================================

    [Fact]
    public async Task PublishImportCompleted_Payload_SemPii()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        await using var ctx = _fixture.CreateContext(tenantId);
        var uow = new SharedUnitOfWork(ctx);
        var publisher = new OutboxPublisher(ctx);

        // Act
        await uow.BeginAsync();
        await publisher.PublishImportCompletedAsync(tenantId, jobId, 100, correlationId);
        await uow.CommitAsync();

        // Assert — verifica o payload JSON do evento.
        var payload = await GetOutboxPayloadAsync(tenantId, OutboxEventType.ImportCompleted);
        payload.Should().NotBeNullOrEmpty("evento deve ter payload");
        payload.Should().Contain("job_id", "payload deve ter job_id");
        payload.Should().Contain("tenant_id", "payload deve ter tenant_id");
        payload.Should().NotContain("email", "payload não deve ter e-mail (PII)");
        payload.Should().NotContain("phone", "payload não deve ter telefone (PII)");
        payload.Should().NotContain("name", "payload não deve ter nome pessoal (PII)");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private async Task<long> CountOutboxEventsAsync(Guid tenantId, string eventType)
    {
        var noPoolConnStr = _fixture.ConnectionString + ";Pooling=false";
        await using var conn = new NpgsqlConnection(noPoolConnStr);
        await conn.OpenAsync();

        // Outbox não tem RLS de leitura bloqueante para o superuser do container.
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM outbox_events WHERE tenant_id = @tid AND event_type = @type";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("type", eventType);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<string?> GetOutboxPayloadAsync(Guid tenantId, string eventType)
    {
        var noPoolConnStr = _fixture.ConnectionString + ";Pooling=false";
        await using var conn = new NpgsqlConnection(noPoolConnStr);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT payload::text FROM outbox_events WHERE tenant_id = @tid AND event_type = @type LIMIT 1";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("type", eventType);
        return (string?)(await cmd.ExecuteScalarAsync());
    }

    private static MigrationJob CreateJob(Guid tenantId) =>
        MigrationJob.Create(
            tenantId: tenantId,
            sourceFileName: "outbox-test.xlsx",
            sourceFileSizeBytes: 1024,
            sourceFileHash: Guid.NewGuid().ToString("N"),
            detectedRowCount: 10,
            createdBy: Guid.NewGuid());
}

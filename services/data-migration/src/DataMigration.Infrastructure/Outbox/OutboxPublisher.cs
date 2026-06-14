using System.Text.Json;
using DataMigration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DataMigration.Infrastructure.Outbox;

/// <summary>
/// Publica eventos de domínio na tabela <c>outbox_events</c> (padrão Outbox, TASK-20).
///
/// Toda escrita ocorre dentro da transação ativa do <see cref="MigrationDbContext"/>,
/// garantindo atomicidade: se a transação fizer rollback, o evento também é descartado
/// (sem dual-write, DD-001, design §6.3).
///
/// Propriedades (design §6.6, RNF 4):
/// - Registro append-only: sem UPDATE ou DELETE pós-emissão.
/// - Payload sem PII: contém apenas IDs e metadados técnicos.
/// - <c>correlation_id</c> e <c>causation_id</c> rastreiam a cadeia de eventos.
///
/// O relay de <c>outbox_events</c> para Cloud Pub/Sub é responsabilidade de
/// componente externo (Outbox Relay Worker — fora do escopo deste módulo).
///
/// Rastreia: design §6.3, §6.6, §9, RNF 4, DD-001, TASK-20.
/// </summary>
public sealed class OutboxPublisher
{
    private readonly MigrationDbContext _context;

    /// <summary>
    /// Inicializa o publisher com o contexto EF Core da transação ativa.
    /// </summary>
    public OutboxPublisher(MigrationDbContext context)
    {
        _context = context;
    }

    // =========================================================================
    // Eventos de import
    // =========================================================================

    /// <summary>
    /// Publica <c>ImportCompleted</c> na mesma transação do import (DD-001).
    /// </summary>
    /// <param name="tenantId">Tenant proprietário do job.</param>
    /// <param name="jobId">ID do job concluído.</param>
    /// <param name="rowsProcessed">Total de linhas processadas com sucesso.</param>
    /// <param name="correlationId">ID de correlação para rastreio distribuído.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    public Task PublishImportCompletedAsync(
        Guid tenantId,
        Guid jobId,
        int rowsProcessed,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            job_id = jobId,
            tenant_id = tenantId,
            rows_processed = rowsProcessed,
            completed_at = DateTimeOffset.UtcNow
        };

        return InsertEventAsync(
            tenantId: tenantId,
            eventType: OutboxEventType.ImportCompleted,
            payload: payload,
            correlationId: correlationId,
            causationId: jobId,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Publica <c>DryRunCompleted</c> após simulação sem efeito colateral (Req 2.1).
    /// </summary>
    public Task PublishDryRunCompletedAsync(
        Guid tenantId,
        Guid jobId,
        int rowsSimulated,
        int warningCount,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            job_id = jobId,
            tenant_id = tenantId,
            rows_simulated = rowsSimulated,
            warning_count = warningCount,
            completed_at = DateTimeOffset.UtcNow
        };

        return InsertEventAsync(
            tenantId: tenantId,
            eventType: OutboxEventType.DryRunCompleted,
            payload: payload,
            correlationId: correlationId,
            causationId: jobId,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Publica <c>ImportRolledBack</c> após falha com rollback (caminho de falha do import).
    ///
    /// ATENÇÃO: este método deve ser chamado em uma nova transação (após o rollback
    /// da transação de import), pois a transação original foi revertida.
    /// </summary>
    public Task PublishImportRolledBackAsync(
        Guid tenantId,
        Guid jobId,
        string failureReason,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            job_id = jobId,
            tenant_id = tenantId,
            failure_reason = failureReason,
            rolled_back_at = DateTimeOffset.UtcNow
        };

        return InsertEventAsync(
            tenantId: tenantId,
            eventType: OutboxEventType.ImportRolledBack,
            payload: payload,
            correlationId: correlationId,
            causationId: jobId,
            cancellationToken: cancellationToken);
    }

    // =========================================================================
    // Inserção atômica no Outbox
    // =========================================================================

    private async Task InsertEventAsync(
        Guid tenantId,
        string eventType,
        object payload,
        Guid correlationId,
        Guid causationId,
        CancellationToken cancellationToken)
    {
        var payloadJson = JsonSerializer.Serialize(payload);

        // Usa SQL raw para garantir INSERT direto sem interferência de Global Query Filter
        // (outbox_events tem RLS mas o INSERT usa SET app.current_tenant já configurado).
        await _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO outbox_events
                (id, tenant_id, event_type, payload, correlation_id, causation_id, created_at)
            VALUES
                (gen_random_uuid(), {0}, {1}, {2}::jsonb, {3}, {4}, now())
            """,
            parameters: [tenantId, eventType, payloadJson, correlationId, causationId],
            cancellationToken: cancellationToken);
    }
}

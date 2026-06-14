namespace ActivityManagement.Infrastructure.Audit;

using ActivityManagement.Application.Ports;
using ActivityManagement.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="IAuditPublisher"/> que grava entradas de auditoria em
/// <c>audit_logs</c> (append-only) na mesma transação EF Core da operação de negócio.
/// A tabela <c>audit_logs</c> é protegida por trigger de imutabilidade
/// (<c>trg_audit_logs_immutable</c>) e <c>REVOKE UPDATE, DELETE, TRUNCATE</c>
/// aplicados pela migration <c>AddAuditImmutability</c> (RNF 2, design §6.6).
/// O <c>DeltaJson</c> é sempre mascarado pelo <see cref="PiiMasker"/> antes de persistir (RNF 7.2).
/// Mapeia: TASK-16, RNF 2, DD-009, design §6.6.
/// </summary>
internal sealed class AuditPublisher : IAuditPublisher
{
    private readonly ActivityManagementDbContext _db;

    /// <summary>Inicializa o publisher com o contexto EF corrente.</summary>
    public AuditPublisher(ActivityManagementDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public Task PublishAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // Garante que o delta_json não contém PII antes de persistir (RNF 7.2)
        var safeDeltaJson = PiiMasker.MaskJson(entry.DeltaJson);

        var log = new AuditLog
        {
            TenantId      = entry.TenantId,
            UserId        = entry.UserId,
            EntityType    = entry.EntityType,
            EntityId      = entry.EntityId,
            Action        = entry.Action,
            DeltaJson     = safeDeltaJson,
            CorrelationId = entry.CorrelationId,
            CreatedAt     = DateTimeOffset.UtcNow,
        };

        _db.AuditLogs.Add(log);
        return Task.CompletedTask;
    }
}

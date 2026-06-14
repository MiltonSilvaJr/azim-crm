using System.Text.Json;
using PartnerManagement.Application.Ports;
using PartnerManagement.Infrastructure.Outbox;
using PartnerManagement.Infrastructure.Persistence;

namespace PartnerManagement.Infrastructure.Audit;

/// <summary>
/// Implementação de <see cref="IAuditPublisher"/> que persiste o registro de auditoria
/// na tabela <c>outbox_messages</c> (append-only) na mesma transação da escrita de domínio.
/// Aplica <see cref="PartnerPiiMasker"/> ao <c>delta_json</c> antes de persistir (DD-008, RNF 4).
/// Não realiza DELETE nem UPDATE — auditoria é imutável (rule audit-immutability.md, RNF 3).
/// Mapeia: RNF 2, RNF 3, RNF 4, design §6.6, TASK-18.
/// </summary>
public sealed class AuditPublisher : IAuditPublisher
{
    private readonly PartnerManagementDbContext _dbContext;
    private readonly PartnerPiiMasker _piiMasker;

    /// <summary>
    /// Inicializa o publisher de auditoria.
    /// </summary>
    /// <param name="dbContext">DbContext scoped para persistência na mesma transação.</param>
    /// <param name="piiMasker">Mascarador de PII para o <c>delta_json</c>.</param>
    public AuditPublisher(PartnerManagementDbContext dbContext, PartnerPiiMasker piiMasker)
    {
        _dbContext = dbContext;
        _piiMasker = piiMasker;
    }

    /// <inheritdoc/>
    public async Task PublishAsync(
        string entityType,
        Guid entityId,
        Guid tenantId,
        string action,
        string deltaJson,
        Guid actorId,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        // Mascara PII no delta_json antes de persistir (DD-008, RNF 4)
        string maskedDelta = _piiMasker.MaskJson(deltaJson);

        // Payload do evento de auditoria — sem PII em claro
        var auditPayload = new
        {
            entityType,
            entityId,
            tenantId,
            action,
            deltaJson = maskedDelta,
            actorId,
            correlationId,
            occurredAt = DateTimeOffset.UtcNow
        };

        string payloadJson = JsonSerializer.Serialize(auditPayload);

        OutboxMessage outboxMessage = OutboxMessage.Create(
            tenantId,
            eventType: $"audit.{entityType.ToLowerInvariant()}.{action.ToLowerInvariant()}",
            payloadJson: payloadJson,
            occurredAt: DateTimeOffset.UtcNow);

        // Persiste na mesma transação via DbContext (append-only — sem UPDATE/DELETE pela role app)
        await _dbContext.OutboxMessages.AddAsync(outboxMessage, cancellationToken).ConfigureAwait(false);
    }
}

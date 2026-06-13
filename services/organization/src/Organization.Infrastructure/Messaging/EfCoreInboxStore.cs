using Microsoft.EntityFrameworkCore;
using Organization.Application.Ports;
using Organization.Infrastructure.Persistence;

namespace Organization.Infrastructure.Messaging;

/// <summary>
/// Implementação de <see cref="IInboxStore"/> via EF Core.
/// Persiste em <c>inbox_messages</c> para deduplicação idempotente de mensagens consumidas (§6.6).
/// A PK composta <c>(message_id, tenant_id)</c> garante unicidade sem colisão entre tenants.
/// </summary>
public sealed class EfCoreInboxStore : IInboxStore
{
    private readonly OrganizationDbContext _context;

    /// <summary>Inicializa o inbox store com o contexto de banco.</summary>
    public EfCoreInboxStore(OrganizationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<bool> IsProcessedAsync(
        string messageId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await _context.InboxMessages
            .AsNoTracking()
            .AnyAsync(
                m => m.MessageId == messageId && m.TenantId == tenantId,
                cancellationToken);
    }

    /// <inheritdoc/>
    public async Task MarkProcessedAsync(
        string messageId,
        Guid tenantId,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        // Verifica se já foi processado antes de inserir (idempotência).
        // Em concorrência, a constraint UNIQUE da PK composta vai rejeitar duplicata.
        var alreadyExists = await _context.InboxMessages
            .AsNoTracking()
            .AnyAsync(
                m => m.MessageId == messageId && m.TenantId == tenantId,
                cancellationToken);

        if (alreadyExists)
            return;

        var message = new InboxMessage
        {
            MessageId = messageId,
            TenantId = tenantId,
            EventType = eventType,
            ProcessedAt = DateTimeOffset.UtcNow,
        };

        _context.InboxMessages.Add(message);
    }
}

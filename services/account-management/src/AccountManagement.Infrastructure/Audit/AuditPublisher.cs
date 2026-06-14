using System.Text.Json;
using AccountManagement.Application.Ports;
using AccountManagement.Domain.Accounts.Events;
using AccountManagement.Domain.Shared;
using AccountManagement.Infrastructure.Persistence;

namespace AccountManagement.Infrastructure.Audit;

/// <summary>
/// Implementação de <see cref="IAuditPublisher"/> que grava em <c>audit_logs</c> (append-only).
///
/// O <c>delta_json</c> é mascarado pelo <see cref="PiiMasker"/> antes de persistir (DD-003).
/// Segunda chamada com mesmos dados gera nova linha — não há deduplicação neste publisher
/// (append-only por definição — design §7).
///
/// Nunca executa UPDATE/DELETE em <c>audit_logs</c>.
///
/// Mapeia: design §6.6, IAuditPublisher, Req 8, RNF 8, DD-003, DD-007, TASK-11.
/// </summary>
internal sealed class AuditPublisher : IAuditPublisher
{
    private readonly AccountManagementDbContext _context;
    private readonly PiiMasker _piiMasker;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    public AuditPublisher(AccountManagementDbContext context, PiiMasker piiMasker)
    {
        _context = context;
        _piiMasker = piiMasker;
    }

    /// <inheritdoc />
    public async Task PublishAsync(
        IDomainEvent domainEvent,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var (entityType, entityId, action, rawDelta) = ExtractAuditInfo(domainEvent, actorId);
        var maskedDelta = _piiMasker.MaskContactDelta(rawDelta);

        var tenantId = ExtractTenantId(domainEvent);

        var entry = new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = actorId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            DeltaJson = maskedDelta,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.AuditLogs.Add(entry);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // =========================================================================
    // Helpers privados
    // =========================================================================

    private static (string entityType, Guid entityId, string action, string rawDelta)
        ExtractAuditInfo(IDomainEvent domainEvent, Guid actorId)
    {
        return domainEvent switch
        {
            AccountCreated e => (
                "Account", e.AccountId, "created",
                JsonSerializer.Serialize(new { normalizedName = e.NormalizedName }, JsonOptions)),

            AccountUpdated e => (
                "Account", e.AccountId, "updated",
                JsonSerializer.Serialize(new { changedFields = e.ChangedFields }, JsonOptions)),

            ContactLinked e => (
                "Contact", e.ContactId, e.Action,
                e.MaskedDelta), // já mascarado pelo domínio (DD-003)

            ContactForgotten e => (
                "Contact", e.ContactId, "forgotten",
                JsonSerializer.Serialize(
                    new { requestedBy = e.RequestedBy, contactId = e.ContactId }, JsonOptions)),

            _ => (
                "Unknown", Guid.Empty, "unknown",
                JsonSerializer.Serialize(new { eventType = domainEvent.GetType().Name }, JsonOptions))
        };
    }

    private static Guid ExtractTenantId(IDomainEvent domainEvent) => domainEvent switch
    {
        AccountCreated e => e.TenantId,
        AccountUpdated e => e.TenantId,
        ContactLinked e => e.TenantId,
        ContactForgotten e => e.TenantId,
        _ => Guid.Empty
    };
}

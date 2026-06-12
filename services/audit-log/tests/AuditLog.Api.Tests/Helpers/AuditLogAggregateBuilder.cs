using AuditLog.Domain.Aggregates;
using AuditLog.Domain.ValueObjects;

namespace AuditLog.Api.Tests.Helpers;

/// <summary>
/// Builder de <see cref="AuditLogAggregate"/> para uso em testes.
/// Reconstitui instâncias diretamente sem interagir com banco de dados.
/// </summary>
public static class AuditLogAggregateBuilder
{
    /// <summary>
    /// Constrói um <see cref="AuditLogAggregate"/> com valores padronizados.
    /// </summary>
    public static AuditLogAggregate Build(
        Guid? id = null,
        Guid? tenantId = null,
        Guid? actorId = null,
        string? entityType = null,
        Guid? entityId = null,
        AuditAction action = AuditAction.Update,
        DateTimeOffset? createdAt = null)
    {
        var delta = AuditDelta.ForUpdate(new Dictionary<string, AuditAttributeChange>
        {
            ["stage_id"] = new AuditAttributeChange(
                "uuid-before",
                "uuid-after")
        }.AsReadOnly());

        return AuditLogAggregate.Reconstitute(
            id: AuditLogId.From(id ?? Guid.NewGuid()),
            tenantId: TenantId.From(tenantId ?? AuditApiFactory.DefaultTenantId),
            actorId: ActorId.From(actorId ?? AuditApiFactory.DefaultUserId),
            entityReference: EntityReference.Create(
                entityType ?? "Opportunity",
                entityId ?? AuditApiFactory.DefaultEntityId),
            action: action,
            delta: delta,
            createdAt: createdAt ?? DateTimeOffset.UtcNow);
    }
}

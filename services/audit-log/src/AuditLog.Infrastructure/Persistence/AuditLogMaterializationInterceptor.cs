using AuditLog.Domain.Aggregates;
using AuditLog.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AuditLog.Infrastructure.Persistence;

/// <summary>
/// Interceptor de materialização EF Core que hidrata <see cref="AuditLogAggregate.EntityReference"/>
/// a partir das shadow properties <c>EntityType</c> e <c>EntityId</c> após o carregamento do banco.
/// Necessário porque <see cref="AuditLog.Domain.ValueObjects.EntityReference"/> é um record imutável
/// com construtor privado, mapeado via shadow properties.
/// </summary>
internal sealed class AuditLogMaterializationInterceptor : IMaterializationInterceptor
{
    public object InitializedInstance(MaterializationInterceptionData materializationData, object instance)
    {
        if (instance is not AuditLogAggregate auditLog)
            return instance;

        var entityType = materializationData.Context
            .Entry(auditLog)
            .Property<string>("EntityType")
            .CurrentValue;

        var entityId = materializationData.Context
            .Entry(auditLog)
            .Property<Guid>("EntityId")
            .CurrentValue;

        // Usa Reconstitute para injetar o EntityReference no aggregate imutável
        return AuditLogAggregate.Reconstitute(
            auditLog.Id,
            auditLog.TenantId,
            auditLog.ActorId,
            EntityReference.Create(entityType, entityId),
            auditLog.Action,
            auditLog.Delta,
            auditLog.CreatedAt);
    }
}

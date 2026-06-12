using AuditLog.Domain.Abstractions;
using AuditLog.Domain.ValueObjects;

namespace AuditLog.Domain.Aggregates;

/// <summary>
/// Aggregate root do módulo de auditoria.
/// Representa um registro imutável e append-only de uma operação de escrita
/// em entidade de negócio do Azim CRM.
/// <para>
/// Invariantes protegidas:
/// <list type="bullet">
/// <item><description>Todos os campos obrigatórios são preenchidos na criação.</description></item>
/// <item><description><see cref="CreatedAt"/> é sempre definido pelo servidor via <see cref="IClock"/>; nunca pelo chamador.</description></item>
/// <item><description>Nenhum método de mutação pública existe; o aggregate é imutável após criação (append-only, REQ-002.5, RNF-001).</description></item>
/// <item><description>PII deve estar mascarada no <see cref="Delta"/> antes da construção (garantia centralizada no AuditService, REQ-004).</description></item>
/// </list>
/// </para>
/// </summary>
public sealed class AuditLogAggregate
{
    /// <summary>Identificador único do registro de auditoria.</summary>
    public AuditLogId Id { get; private init; } = null!;

    /// <summary>Tenant ao qual o registro pertence. Chave de RLS.</summary>
    public TenantId TenantId { get; private init; } = null!;

    /// <summary>Autor da operação (usuário humano ou de sistema).</summary>
    public ActorId ActorId { get; private init; } = null!;

    /// <summary>
    /// Referência à entidade auditada (tipo + identificador).
    /// Construída a partir de <see cref="EntityTypePersisted"/> e <see cref="EntityIdPersisted"/>,
    /// que são os campos mapeados pelo EF Core.
    /// </summary>
    public EntityReference EntityReference =>
        EntityReference.Create(EntityTypePersisted, EntityIdPersisted);

    /// <summary>
    /// Tipo da entidade auditada — persisted field mapeado pelo EF Core para a coluna <c>entity_type</c>.
    /// Use <see cref="EntityReference"/> para acesso semântico.
    /// </summary>
    public string EntityTypePersisted { get; private set; } = null!;

    /// <summary>
    /// Identificador da entidade auditada — persisted field mapeado pelo EF Core para <c>entity_id</c>.
    /// Use <see cref="EntityReference"/> para acesso semântico.
    /// </summary>
    public Guid EntityIdPersisted { get; private set; }

    /// <summary>Tipo de operação auditada: Create, Update ou Delete.</summary>
    public AuditAction Action { get; private init; }

    /// <summary>Diferencial da entidade com PII já mascarada.</summary>
    public AuditDelta Delta { get; private init; } = null!;

    /// <summary>
    /// Timestamp do servidor no momento da persistência.
    /// Definido exclusivamente pelo <see cref="IClock"/> do servidor (REQ-002.4).
    /// </summary>
    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>Construtor privado — use os factory methods.</summary>
    private AuditLogAggregate() { }

    /// <summary>
    /// Cria um novo registro de auditoria com timestamp derivado do relógio do servidor.
    /// </summary>
    /// <param name="tenantId">Tenant da operação (do contexto autenticado).</param>
    /// <param name="actorId">Autor da operação; nunca vazio (REQ-002.2).</param>
    /// <param name="entityReference">Entidade auditada.</param>
    /// <param name="action">Tipo de operação (Create, Update ou Delete).</param>
    /// <param name="maskedDelta">Delta com PII já mascarada (REQ-004).</param>
    /// <param name="clock">Relógio do servidor para derivar <see cref="CreatedAt"/>.</param>
    /// <returns>Novo <see cref="AuditLogAggregate"/> imutável.</returns>
    /// <exception cref="ArgumentNullException">Se qualquer argumento obrigatório for nulo.</exception>
    public static AuditLogAggregate Create(
        TenantId tenantId,
        ActorId actorId,
        EntityReference entityReference,
        AuditAction action,
        AuditDelta maskedDelta,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(actorId);
        ArgumentNullException.ThrowIfNull(entityReference);
        ArgumentNullException.ThrowIfNull(maskedDelta);
        ArgumentNullException.ThrowIfNull(clock);

        return new AuditLogAggregate
        {
            Id = AuditLogId.New(),
            TenantId = tenantId,
            ActorId = actorId,
            EntityTypePersisted = entityReference.EntityType,
            EntityIdPersisted = entityReference.EntityId,
            Action = action,
            Delta = maskedDelta,
            CreatedAt = clock.UtcNow
        };
    }

    /// <summary>
    /// Reconstitui um <see cref="AuditLogAggregate"/> a partir de dados persistidos (somente leitura).
    /// Usado exclusivamente pela camada de infraestrutura ao hidratar registros do banco.
    /// </summary>
    /// <returns>Instância reconstituída com os dados fornecidos.</returns>
    /// <exception cref="ArgumentNullException">Se qualquer argumento obrigatório for nulo.</exception>
    public static AuditLogAggregate Reconstitute(
        AuditLogId id,
        TenantId tenantId,
        ActorId actorId,
        EntityReference entityReference,
        AuditAction action,
        AuditDelta delta,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(actorId);
        ArgumentNullException.ThrowIfNull(entityReference);
        ArgumentNullException.ThrowIfNull(delta);

        return new AuditLogAggregate
        {
            Id = id,
            TenantId = tenantId,
            ActorId = actorId,
            EntityTypePersisted = entityReference.EntityType,
            EntityIdPersisted = entityReference.EntityId,
            Action = action,
            Delta = delta,
            CreatedAt = createdAt
        };
    }
}

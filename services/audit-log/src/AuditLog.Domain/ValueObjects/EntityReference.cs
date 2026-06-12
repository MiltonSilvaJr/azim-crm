namespace AuditLog.Domain.ValueObjects;

/// <summary>
/// Referência à entidade de negócio auditada.
/// Combina <c>entity_type</c> (string ≤ 50 chars) e <c>entity_id</c> (Guid).
/// Imutável; igualdade por valor.
/// </summary>
public sealed record EntityReference
{
    /// <summary>
    /// Nome do tipo da entidade auditada (ex.: <c>Opportunity</c>, <c>Contact</c>).
    /// Máximo de 50 caracteres.
    /// </summary>
    public string EntityType { get; }

    /// <summary>Identificador único da instância da entidade.</summary>
    public Guid EntityId { get; }

    private EntityReference(string entityType, Guid entityId)
    {
        EntityType = entityType;
        EntityId = entityId;
    }

    /// <summary>
    /// Cria uma <see cref="EntityReference"/> validada.
    /// </summary>
    /// <param name="entityType">Tipo da entidade; não nulo, não vazio, máx. 50 chars.</param>
    /// <param name="entityId">Identificador da entidade; não pode ser <see cref="Guid.Empty"/>.</param>
    /// <exception cref="ArgumentNullException">Se <paramref name="entityType"/> for nulo.</exception>
    /// <exception cref="ArgumentException">
    /// Se <paramref name="entityType"/> for vazio ou exceder 50 caracteres,
    /// ou se <paramref name="entityId"/> for <see cref="Guid.Empty"/>.
    /// </exception>
    public static EntityReference Create(string entityType, Guid entityId)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        if (entityType.Length == 0)
            throw new ArgumentException(
                "O tipo de entidade não pode ser vazio (REQ-002.1).",
                nameof(entityType));

        if (entityType.Length > 50)
            throw new ArgumentException(
                $"O tipo de entidade não pode exceder 50 caracteres (tamanho atual: {entityType.Length}).",
                nameof(entityType));

        if (entityId == Guid.Empty)
            throw new ArgumentException(
                "O identificador da entidade não pode ser um Guid vazio.",
                nameof(entityId));

        return new(entityType, entityId);
    }
}

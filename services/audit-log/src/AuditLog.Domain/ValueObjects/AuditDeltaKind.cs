namespace AuditLog.Domain.ValueObjects;

/// <summary>
/// Discriminante do <see cref="AuditDelta"/>: indica qual variante do delta está preenchida.
/// </summary>
public enum AuditDeltaKind
{
    /// <summary>Delta de criação — somente estado <c>After</c> está presente.</summary>
    Create,

    /// <summary>Delta de atualização — somente <c>Changes</c> (atributos alterados) está presente.</summary>
    Update,

    /// <summary>Delta de exclusão — somente estado <c>Before</c> está presente.</summary>
    Delete
}

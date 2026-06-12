namespace AuditLog.Contracts;

/// <summary>
/// Representa a operação de escrita que gerou uma entrada de auditoria.
/// Serializado como string em lowercase (ex.: <c>create</c>, <c>update</c>, <c>delete</c>).
/// <para>
/// Convensão de serialização: configure <c>JsonStringEnumConverter</c> com
/// <c>JsonNamingPolicy.CamelCase</c> no host de serialização para garantir
/// os valores lowercase nos contratos HTTP e de mensageria.
/// </para>
/// </summary>
public enum AuditAction
{
    /// <summary>Criação de uma nova entidade.</summary>
    Create,

    /// <summary>Atualização de uma entidade existente.</summary>
    Update,

    /// <summary>Exclusão de uma entidade existente.</summary>
    Delete
}

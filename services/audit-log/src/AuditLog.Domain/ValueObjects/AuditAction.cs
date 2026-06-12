namespace AuditLog.Domain.ValueObjects;

/// <summary>
/// Operação de escrita que gerou o registro de auditoria.
/// Espelho semântico de <see cref="AuditLog.Contracts.AuditAction"/>
/// mantendo o domínio desacoplado do contrato público.
/// Serializado como string em lowercase nos contratos externos.
/// </summary>
public enum AuditAction
{
    /// <summary>Criação de uma nova instância de entidade.</summary>
    Create,

    /// <summary>Atualização de uma instância existente.</summary>
    Update,

    /// <summary>Exclusão de uma instância existente.</summary>
    Delete
}

namespace AuditLog.Application.Abstractions;

/// <summary>
/// Fornece o identificador do tenant do contexto autenticado corrente.
/// Nunca aceito do chamador externo — derivado do token JWT (REQ-005.3).
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Identificador do tenant autenticado.
    /// Retorna <see langword="null"/> quando não há contexto de tenant (configuração inválida).
    /// </summary>
    Guid? TenantId { get; }
}

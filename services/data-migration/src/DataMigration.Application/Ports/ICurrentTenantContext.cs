namespace DataMigration.Application.Ports;

/// <summary>
/// Porta de leitura do contexto de tenant corrente.
///
/// Usado pelo <c>TenantContextBehavior</c> para verificar que
/// <c>app.current_tenant</c> está definido antes de qualquer operação (ADR-0001).
///
/// Implementação em Infrastructure (resolvido da requisição HTTP).
///
/// Rastreia: design §5.4, ADR-0001, TASK-12.
/// </summary>
public interface ICurrentTenantContext
{
    /// <summary>
    /// ID do tenant corrente. Retorna <c>null</c> quando não definido.
    /// </summary>
    Guid? TenantId { get; }

    /// <summary>
    /// ID do usuário corrente. Retorna <c>null</c> quando não autenticado.
    /// </summary>
    Guid? UserId { get; }

    /// <summary>
    /// Papel do usuário corrente no tenant (ex: "PlatformOperator", "TenantAdmin").
    /// </summary>
    string? Role { get; }
}

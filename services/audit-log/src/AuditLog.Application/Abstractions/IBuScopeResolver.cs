namespace AuditLog.Application.Abstractions;

/// <summary>
/// Resolve o escopo de Business Units (BUs) acessíveis pelo usuário autenticado corrente.
/// Utilizado pelo <c>ListAuditLogsHandler</c> para aplicar filtro de BU ao papel <c>GestorBU</c> (DD-008).
/// <para>
/// Implementação concreta pendente (Wave futura, RISK-AUDIT-05):
/// a resolução depende do read model de entidades auditadas e da relação entity_id ↔ BU.
/// </para>
/// </summary>
public interface IBuScopeResolver
{
    /// <summary>
    /// Retorna o conjunto de <c>entity_id</c> acessíveis pelo usuário corrente dentro do escopo de BU.
    /// </summary>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>
    /// Conjunto de <c>entity_id</c> permitidos para <c>GestorBU</c>;
    /// <see langword="null"/> indica acesso irrestrito (papel <c>TenantAdmin</c>).
    /// </returns>
    Task<IReadOnlySet<Guid>?> ResolveAsync(CancellationToken ct);
}

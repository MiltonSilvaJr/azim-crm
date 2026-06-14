namespace AccountManagement.Application.Behaviors;

/// <summary>
/// Contexto de tenant resolvido por <see cref="TenantScopeBehavior{TRequest,TResponse}"/>
/// a partir do JWT autenticado. Injetado como serviço scoped no container DI.
///
/// O <c>TenantId</c> é resolvido antes de qualquer operação de dados para garantir
/// o isolamento multi-tenant em profundidade (DD-002, ADR-0001).
///
/// Mapeia: design §5.4, design §14, Req 10, RNF 5, DD-002.
/// </summary>
public sealed class TenantContext
{
    private Guid? _tenantId;

    /// <summary>Identificador do tenant autenticado. Nulo antes de <see cref="SetTenant"/>.</summary>
    public Guid? TenantId => _tenantId;

    /// <summary>
    /// Define o tenant do contexto. Chamado pelo <see cref="TenantScopeBehavior{TRequest,TResponse}"/>.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant extraído do JWT.</param>
    public void SetTenant(Guid tenantId) => _tenantId = tenantId;

    /// <summary>
    /// Retorna o <see cref="TenantId"/> ou lança <see cref="InvalidOperationException"/>
    /// quando o contexto não foi inicializado.
    /// </summary>
    public Guid GetRequiredTenantId() =>
        _tenantId ?? throw new InvalidOperationException(
            "O contexto de tenant não foi inicializado. O TenantScopeBehavior deve ser executado antes do handler.");
}

namespace Organization.Application.Ports;

/// <summary>
/// Port de métricas do módulo Organization.
/// Expõe apenas os incrementos necessários aos handlers da camada Application.
/// A implementação concreta usa <c>System.Diagnostics.Metrics</c> na Infrastructure (design §11).
/// Sem PII em nenhum parâmetro.
/// </summary>
public interface IOrganizationMetrics
{
    /// <summary>Incrementa o contador de convites emitidos com sucesso.</summary>
    void IncrementUsersInvited();

    /// <summary>Incrementa o contador de usuários desativados.</summary>
    void IncrementUsersDeactivated();

    /// <summary>Incrementa o contador de Business Units criadas.</summary>
    void IncrementBuCreated();

    /// <summary>Incrementa o contador de acertos no cache de memberships.</summary>
    void IncrementMembershipCacheHit();

    /// <summary>Incrementa o contador de erros/misses no cache de memberships.</summary>
    void IncrementMembershipCacheMiss();

    /// <summary>
    /// Incrementa o contador de violações RLS detectadas.
    /// Alerta imediato deve ser emitido pelo observability quando este valor &gt; 0.
    /// </summary>
    void IncrementTenantRlsViolation();
}

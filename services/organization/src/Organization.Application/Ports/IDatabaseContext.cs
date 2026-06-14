namespace Organization.Application.Ports;

/// <summary>
/// Port de saída para controle de transação e propagação do contexto de tenant para o banco.
/// Implementado pelo <c>OrganizationDbContext</c> na Infrastructure.
/// </summary>
public interface IDatabaseContext
{
    /// <summary>
    /// Define o tenant corrente na conexão de banco via <c>SET app.current_tenant</c>.
    /// Necessário para ativar a política RLS (DEC-006, ADR-0001).
    /// </summary>
    /// <param name="tenantId">Identificador do tenant.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task SetTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Inicia uma transação de banco de dados.</summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Comita a transação corrente.</summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Desfaz a transação corrente (rollback).</summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

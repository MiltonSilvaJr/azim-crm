namespace TenantAdministration.Application.Ports;

/// <summary>
/// Porta de saída que abstrai a unidade de trabalho transacional.
/// O <c>TransactionBehavior</c> usa esta porta para gerenciar transações
/// sem acoplamento direto ao EF Core.
/// Implementação concreta vem na Onda 4 (Infrastructure).
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Inicia uma transação de banco de dados, opcionalmente aplicando
    /// <c>SET app.current_tenant</c> para o RLS do plano de tenant.
    /// </summary>
    /// <param name="tenantId">
    /// ID do tenant para configurar o contexto de RLS. Nulo no plano de plataforma.
    /// </param>
    /// <param name="ct">Token de cancelamento.</param>
    Task BeginAsync(Guid? tenantId, CancellationToken ct = default);

    /// <summary>Confirma a transação corrente.</summary>
    Task CommitAsync(CancellationToken ct = default);

    /// <summary>Desfaz a transação corrente em caso de erro.</summary>
    Task RollbackAsync(CancellationToken ct = default);
}

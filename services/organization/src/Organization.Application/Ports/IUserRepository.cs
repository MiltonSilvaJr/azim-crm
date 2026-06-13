using Organization.Domain.Aggregates;

namespace Organization.Application.Ports;

/// <summary>
/// Port de saída para o repositório de <see cref="User"/>.
/// Implementado na Infrastructure via EF Core.
/// </summary>
public interface IUserRepository
{
    /// <summary>Persiste (insert ou update) um usuário.</summary>
    Task SaveAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>Recupera um usuário pelo identificador único no tenant corrente.</summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera um usuário pelo e-mail no tenant corrente.
    /// Retorna <c>null</c> quando não encontrado.
    /// </summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna os TAdmins ativos do tenant corrente.
    /// Utilizado pelo <c>ITenantAdminCounter</c> e pela <c>LastTenantAdminPolicy</c>.
    /// </summary>
    Task<IReadOnlyList<User>> GetActiveTenantAdminsAsync(CancellationToken cancellationToken = default);

    /// <summary>Retorna os usuários ativos do tenant (paginados).</summary>
    Task<IReadOnlyList<User>> ListActiveAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}

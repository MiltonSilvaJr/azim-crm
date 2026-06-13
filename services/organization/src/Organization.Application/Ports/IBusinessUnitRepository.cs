using Organization.Domain.Aggregates;

namespace Organization.Application.Ports;

/// <summary>
/// Port de saída para o repositório de <see cref="BusinessUnit"/>.
/// Implementado na Infrastructure via EF Core.
/// O <c>tenant_id</c> é transparente via <see cref="ITenantContext"/> e global query filter.
/// </summary>
public interface IBusinessUnitRepository
{
    /// <summary>
    /// Persiste (insert ou update) uma Business Unit.
    /// </summary>
    Task SaveAsync(BusinessUnit businessUnit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera uma BU pelo identificador único.
    /// Retorna <c>null</c> quando não encontrada no tenant corrente.
    /// </summary>
    Task<BusinessUnit?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se existe BU com o nome informado no tenant corrente (case-insensitive).
    /// Utilizada para validar unicidade antes de criar ou renomear.
    /// </summary>
    Task<bool> ExistsByNameAsync(string normalizedName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna todas as BUs ativas do tenant corrente (paginadas).
    /// </summary>
    Task<IReadOnlyList<BusinessUnit>> ListActiveAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}

using DataMigration.Domain.Aggregates;

namespace DataMigration.Application.Ports;

/// <summary>
/// Porta de persistência do agregado <see cref="MigrationJob"/>.
///
/// Implementação em Infrastructure (EF Core). Interface em Application para
/// respeitar a regra de dependência Clean Architecture.
///
/// Todas as operações são escopadas ao <c>tenant_id</c> do contexto corrente
/// (ADR-0001, EF Core Global Query Filter).
///
/// Rastreia: design §6.1, §7, ADR-0001, TASK-08.
/// </summary>
public interface IMigrationJobRepository
{
    /// <summary>
    /// Persiste um novo <see cref="MigrationJob"/> (INSERT).
    /// </summary>
    Task AddAsync(MigrationJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera um <see cref="MigrationJob"/> pelo seu ID.
    /// Retorna <c>null</c> quando não encontrado ou fora do escopo do tenant.
    /// </summary>
    Task<MigrationJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste atualizações do agregado (UPDATE).
    /// </summary>
    Task UpdateAsync(MigrationJob job, CancellationToken cancellationToken = default);
}

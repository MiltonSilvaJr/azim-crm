namespace DataMigration.Application.Ports;

/// <summary>
/// Porta de leitura de configurações organizacionais (organization — DEP-04).
///
/// Permite validar que BUs e estágios estão pré-configurados no tenant
/// antes do import. Retorna <c>null</c> quando não encontrado.
/// Lança <see cref="Exceptions.MigrationDomainException"/> com MIG-ERR-010
/// quando BU/estágio não existir.
///
/// Implementação em Infrastructure (adaptador in-process, DD-001).
///
/// Rastreia: design §6.4, RISK-MIGR-05, MIG-ERR-010, DEP-04, TASK-11.
/// </summary>
public interface IOrganizationReadPort
{
    /// <summary>
    /// Resolve o ID de uma Business Unit pelo nome.
    /// Retorna <c>null</c> quando não configurada no tenant.
    /// </summary>
    Task<Guid?> GetBuIdByNameAsync(
        string buName,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve o ID de um estágio de funil pelo nome.
    /// Retorna <c>null</c> quando não configurado no tenant.
    /// </summary>
    Task<Guid?> GetStageIdByNameAsync(
        string stageName,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve o <c>userId</c> de um owner pelo nome de usuário.
    /// Retorna <c>null</c> quando não encontrado.
    /// </summary>
    Task<Guid?> GetUserIdByNameAsync(
        string userName,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

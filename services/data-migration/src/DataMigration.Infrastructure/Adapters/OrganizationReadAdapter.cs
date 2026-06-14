using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;

namespace DataMigration.Infrastructure.Adapters;

/// <summary>
/// Adaptador in-process da porta <see cref="IOrganizationReadPort"/> (DD-001, design §6.4).
///
/// Simula integração com o módulo organization usando armazenamento
/// em memória configurável por test setup.
///
/// Permite pré-configurar BUs, estágios e usuários para cada tenant.
/// Retorna <c>null</c> quando não encontrado (DEP-04: BUs/estágios devem existir pré-import).
///
/// Rastreia: design §6.4, RISK-MIGR-05, MIG-ERR-010, DEP-04, TASK-18.
/// </summary>
internal sealed class OrganizationReadAdapter : IOrganizationReadPort
{
    // Índice: tenantId + nome de BU → buId.
    private readonly Dictionary<(Guid, string), Guid> _buIds = new();

    // Índice: tenantId + nome de estágio → stageId.
    private readonly Dictionary<(Guid, string), Guid> _stageIds = new();

    // Índice: tenantId + nome de usuário → userId.
    private readonly Dictionary<(Guid, string), Guid> _userIds = new();

    // =========================================================================
    // Configuração de test setup (usado por testes e bootstrapping)
    // =========================================================================

    /// <summary>
    /// Pré-configura uma Business Unit para o tenant (DEP-04).
    /// </summary>
    public void RegisterBu(Guid tenantId, string buName, Guid buId)
    {
        _buIds[(tenantId, buName.Trim())] = buId;
    }

    /// <summary>
    /// Pré-configura um estágio de funil para o tenant (DEP-04).
    /// </summary>
    public void RegisterStage(Guid tenantId, string stageName, Guid stageId)
    {
        _stageIds[(tenantId, stageName.Trim())] = stageId;
    }

    /// <summary>
    /// Pré-configura um usuário para o tenant.
    /// </summary>
    public void RegisterUser(Guid tenantId, string userName, Guid userId)
    {
        _userIds[(tenantId, userName.Trim())] = userId;
    }

    // =========================================================================
    // IOrganizationReadPort
    // =========================================================================

    /// <inheritdoc />
    public Task<Guid?> GetBuIdByNameAsync(
        string buName,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        _buIds.TryGetValue((tenantId, buName.Trim()), out var buId);
        return Task.FromResult<Guid?>(buId == Guid.Empty ? null : buId);
    }

    /// <inheritdoc />
    public Task<Guid?> GetStageIdByNameAsync(
        string stageName,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        _stageIds.TryGetValue((tenantId, stageName.Trim()), out var stageId);
        return Task.FromResult<Guid?>(stageId == Guid.Empty ? null : stageId);
    }

    /// <inheritdoc />
    public Task<Guid?> GetUserIdByNameAsync(
        string userName,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        _userIds.TryGetValue((tenantId, userName.Trim()), out var userId);
        return Task.FromResult<Guid?>(userId == Guid.Empty ? null : userId);
    }
}

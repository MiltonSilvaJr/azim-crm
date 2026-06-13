using Authentication.Application.Ports;
using Authentication.Application.Ports.Results;

namespace Authentication.Infrastructure.Directory;

/// <summary>
/// Implementação nula de <see cref="IUserDirectory"/> para uso em desenvolvimento
/// quando o banco de dados não está disponível.
///
/// Sempre retorna <see langword="null"/> (nenhum usuário encontrado).
/// Deve ser substituído pela implementação real em produção.
///
/// Mapeia: TASK-14, TASK-15, TASK-16.
/// </summary>
public sealed class NullUserDirectory : IUserDirectory
{
    /// <inheritdoc/>
    public Task<UserDirectoryResult?> FindUserAsync(
        string providerUserRef,
        Guid tenantId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<UserDirectoryResult?>(null);

    /// <inheritdoc/>
    public Task<bool> IsEmailActiveAsync(
        string email,
        Guid tenantId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    /// <inheritdoc/>
    public Task<UserDirectoryResult?> FindUserByEmailAsync(
        string email,
        Guid tenantId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<UserDirectoryResult?>(null);
}

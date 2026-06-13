using Authentication.Application.Ports;
using Authentication.Application.Ports.Results;

namespace Authentication.Infrastructure.Directory;

/// <summary>
/// Implementação nula de <see cref="ITenantDirectory"/> para uso em desenvolvimento
/// quando o banco de dados não está disponível.
///
/// Sempre retorna <see langword="null"/> (nenhum tenant resolvido).
/// Deve ser substituído pela implementação real em produção.
///
/// Mapeia: TASK-14, TASK-15.
/// </summary>
public sealed class NullTenantDirectory : ITenantDirectory
{
    /// <inheritdoc/>
    public Task<TenantResolutionResult?> ResolveSlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
        => Task.FromResult<TenantResolutionResult?>(null);
}

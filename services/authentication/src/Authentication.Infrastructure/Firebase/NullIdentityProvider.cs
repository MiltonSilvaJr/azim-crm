using Authentication.Application.Ports;
using Authentication.Application.Ports.Exceptions;
using Authentication.Application.Ports.Results;

namespace Authentication.Infrastructure.Firebase;

/// <summary>
/// Implementação nula de <see cref="IIdentityProvider"/> para uso em desenvolvimento
/// quando o Firebase não está disponível.
///
/// Lança exceção em todas as operações. Deve ser substituído pela implementação
/// real em produção.
///
/// Mapeia: TASK-10, TASK-15, TASK-16.
/// </summary>
public sealed class NullIdentityProvider : IIdentityProvider
{
    /// <inheritdoc/>
    public Task<VerifyTokenResult> VerifyTokenAsync(
        string rawJwt,
        string expectedFirebaseTenant,
        CancellationToken cancellationToken = default)
        => throw new IdentityProviderException("AUTH-ERR-020", "Provedor de identidade não configurado.");

    /// <inheritdoc/>
    public Task RevokeRefreshTokensAsync(
        Guid userIdInTenant,
        CancellationToken cancellationToken = default)
        => throw new IdentityProviderException("AUTH-ERR-020", "Provedor de identidade não configurado.");

    /// <inheritdoc/>
    public Task<ActivationLinkResult> GenerateInviteActivationAsync(
        string email,
        string firebaseTenant,
        CancellationToken cancellationToken = default)
        => throw new IdentityProviderException("AUTH-ERR-020", "Provedor de identidade não configurado.");

    /// <inheritdoc/>
    public Task<ResetLinkResult> GeneratePasswordResetLinkAsync(
        string email,
        string firebaseTenant,
        CancellationToken cancellationToken = default)
        => throw new IdentityProviderException("AUTH-ERR-020", "Provedor de identidade não configurado.");

    /// <inheritdoc/>
    public Task<HealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(HealthStatus.Unhealthy);
}

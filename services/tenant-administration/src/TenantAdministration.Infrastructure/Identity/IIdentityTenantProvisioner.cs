namespace TenantAdministration.Infrastructure.Identity;

/// <summary>
/// Resultado da criação de tenant no Identity Platform.
/// </summary>
/// <param name="IdentityTenantId">Identificador do tenant no Identity Platform.</param>
public sealed record IdentityProvisioningResult(string IdentityTenantId);

/// <summary>
/// Abstração para operações de provisionamento no GCP Identity Platform.
/// Permite substituição por fake em testes sem acesso à cloud real.
/// </summary>
public interface IIdentityTenantProvisioner
{
    /// <summary>
    /// Cria um tenant no Identity Platform de forma idempotente.
    /// </summary>
    /// <param name="slug">Slug do tenant (identificador único).</param>
    /// <param name="adminEmail">E-mail do administrador inicial.</param>
    /// <param name="idempotencyKey">Chave de idempotência para evitar duplicação.</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>Resultado com <c>IdentityTenantId</c> criado.</returns>
    Task<IdentityProvisioningResult> CreateTenantAsync(
        string slug,
        string adminEmail,
        string idempotencyKey,
        CancellationToken ct = default);

    /// <summary>
    /// Remove um tenant do Identity Platform (compensação da saga — design.md §6.4).
    /// </summary>
    /// <param name="identityTenantId">Identificador do tenant no Identity Platform.</param>
    /// <param name="ct">Token de cancelamento.</param>
    Task DeleteTenantAsync(string identityTenantId, CancellationToken ct = default);
}

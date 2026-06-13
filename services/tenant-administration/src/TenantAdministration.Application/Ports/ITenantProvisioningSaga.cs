namespace TenantAdministration.Application.Ports;

/// <summary>
/// Resultado de um provisionamento bem-sucedido.
/// </summary>
/// <param name="TenantId">Identificador único do tenant criado.</param>
/// <param name="IdentityTenantId">Identificador do tenant no Identity Platform.</param>
public sealed record ProvisioningResult(Guid TenantId, string IdentityTenantId);

/// <summary>
/// Porta de saída que abstrai a saga de provisionamento atômico de tenant.
/// Coordena criação no Identity Platform + persistência no banco de dados
/// com compensação em caso de falha (design.md §6.4, Req 12).
/// Implementação concreta vem na Onda 4 (Infrastructure — GcpIdentityPlatformAdapter).
/// </summary>
public interface ITenantProvisioningSaga
{
    /// <summary>
    /// Executa a saga de provisionamento:
    /// (1) Cria tenant no IdP com <paramref name="idempotencyKey"/>;
    /// (2) Persiste o agregado <c>Tenant</c> no banco + Outbox;
    /// (3) Em falha de persistência, compensa deletando o tenant do IdP.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant (pré-gerado pelo handler).</param>
    /// <param name="slug">Slug único e imutável.</param>
    /// <param name="displayName">Nome de exibição.</param>
    /// <param name="timezone">Fuso horário IANA.</param>
    /// <param name="digestTime">Horário do digest.</param>
    /// <param name="adminEmail">E-mail do administrador inicial (semente do IdP).</param>
    /// <param name="idempotencyKey">Chave de idempotência para o IdP evitar duplicação.</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>Resultado do provisionamento com IDs do tenant e do IdP.</returns>
    Task<ProvisioningResult> ExecuteAsync(
        Guid tenantId,
        string slug,
        string displayName,
        string timezone,
        string digestTime,
        string adminEmail,
        string idempotencyKey,
        CancellationToken ct = default);
}

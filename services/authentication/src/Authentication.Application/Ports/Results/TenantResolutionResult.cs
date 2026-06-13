namespace Authentication.Application.Ports.Results;

/// <summary>
/// Resultado da resolução de slug pelo <see cref="ITenantDirectory"/>.
///
/// Mapeia: design.md § 6.1, § 6.7, Req 1.
/// </summary>
public sealed record TenantResolutionResult
{
    /// <summary>Identificador interno do tenant no Azim CRM.</summary>
    public required Guid TenantId { get; init; }

    /// <summary>
    /// Identificador do tenant de identidade no GCP Identity Platform (Firebase multi-tenant).
    /// Usado pela <c>TenantMatchSpec</c> e pelo adapter do IdP.
    /// </summary>
    public required string IdentityTenantId { get; init; }
}

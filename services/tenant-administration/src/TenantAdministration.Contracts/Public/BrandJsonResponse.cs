using TenantAdministration.Contracts.Tenant;

namespace TenantAdministration.Contracts.Public;

/// <summary>
/// Payload público do brand.json — sem dados sensíveis (Req 9.2).
/// Não contém adminEmail, identityTenantId nem dados de plano de plataforma.
/// design.md §8.3 GET /brand/{slug}/brand.json.
/// </summary>
public sealed record BrandJsonResponse(
    string Slug,
    string? LogoUrl,
    string? FaviconUrl,
    ColorsResponse? Colors,
    bool WcagContrastOk,
    DerivedTonesResponse? DerivedTones,
    DateTimeOffset Version);

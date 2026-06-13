namespace TenantAdministration.Contracts.Tenant;

/// <summary>
/// Par de cores do branding.
/// </summary>
public sealed record ColorsResponse(string Primary, string Secondary);

/// <summary>
/// Tons derivados deterministicamente das cores primária e secundária (Req 8).
/// </summary>
public sealed record DerivedTonesResponse(
    string PrimaryHover,
    string PrimaryActive,
    string PrimaryMuted,
    string SecondaryHover,
    string SecondaryActive,
    string SecondaryMuted);

/// <summary>
/// Payload de response para leitura do branding do tenant (GET /api/v1/tenant/branding).
/// Inclui tons derivados (Req 9).
/// design.md §8.2.
/// </summary>
public sealed record BrandingResponse(
    Guid TenantId,
    string? LogoUrl,
    string? FaviconUrl,
    ColorsResponse? Colors,
    bool WcagContrastOk,
    decimal LastContrastRatio,
    DerivedTonesResponse? DerivedTones,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Payload de response para atualização de branding (PUT /api/v1/tenant/branding).
/// design.md §8.2.
/// </summary>
public sealed record UpdateBrandingResponse(
    bool WcagContrastOk,
    decimal ContrastRatio,
    string? LogoUrl,
    string? FaviconUrl,
    DerivedTonesResponse DerivedTones);

using TenantAdministration.Domain.ValueObjects;

namespace TenantAdministration.Domain.Entities;

/// <summary>
/// Entidade interna ao agregado <see cref="TenantAdministration.Domain.Aggregates.Tenant"/>.
/// Representa o branding white-label do tenant (1:1 com Tenant).
/// Só é modificável via <see cref="TenantAdministration.Domain.Aggregates.Tenant.UpdateBranding"/>.
/// Sem repositório próprio; sem comportamento público.
/// </summary>
public sealed class TenantBranding
{
    // Construtor privado sem parâmetros para uso exclusivo do EF Core (materialização).
#pragma warning disable CS8618
    private TenantBranding() { }
#pragma warning restore CS8618

    /// <summary>Tema do branding com os elementos white-label estritos.</summary>
    public BrandingTheme Theme { get; private set; }

    /// <summary>Indica se as cores atendem ao contraste WCAG AA (Req 6.3).</summary>
    public bool WcagContrastOk { get; private set; }

    /// <summary>Última razão de contraste calculada (Req 6.2). Nulo até o primeiro cálculo.</summary>
    public decimal? LastContrastRatio { get; private set; }

    /// <summary>Data da última atualização de branding.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    internal TenantBranding(BrandingTheme theme, bool wcagContrastOk, decimal? contrastRatio, DateTimeOffset updatedAt)
    {
        Theme = theme;
        WcagContrastOk = wcagContrastOk;
        LastContrastRatio = contrastRatio;
        UpdatedAt = updatedAt;
    }

    internal void Update(BrandingTheme theme, bool wcagContrastOk, decimal? contrastRatio, DateTimeOffset updatedAt)
    {
        Theme = theme;
        WcagContrastOk = wcagContrastOk;
        LastContrastRatio = contrastRatio;
        UpdatedAt = updatedAt;
    }
}

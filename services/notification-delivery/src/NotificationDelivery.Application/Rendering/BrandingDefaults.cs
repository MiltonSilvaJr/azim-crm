namespace NotificationDelivery.Application.Rendering;

/// <summary>
/// Valores padrão de branding aplicados quando <see cref="NotificationDelivery.Contracts.BrandingConfig"/>
/// está ausente na <see cref="NotificationDelivery.Contracts.EmailMessage"/>.
///
/// Correspondem ao tema Azim CRM definido em DEC-004 (Req 5.3, design §5.3, TASK-09/ST-03).
/// Nenhum CSS arbitrário é aceito — apenas estas constantes são injetadas como tema padrão.
/// </summary>
public static class BrandingDefaults
{
    /// <summary>Cor primária padrão (#0F4C81 — azul Azim CRM). Formato hex RRGGBB.</summary>
    public const string PrimaryColor = "#0F4C81";

    /// <summary>Cor secundária padrão (#FFFFFF — branco). Formato hex RRGGBB.</summary>
    public const string SecondaryColor = "#FFFFFF";

    /// <summary>URL do logo padrão da plataforma Azim CRM.</summary>
    public const string LogoUrl = "https://assets.azim.com.br/logo/azim-logo.png";
}

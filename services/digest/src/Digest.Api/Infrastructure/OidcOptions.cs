namespace Digest.Api.Infrastructure;

/// <summary>
/// Opções de configuração OIDC/WIF para o trigger do digest (design §10, RNF 7.1).
/// Lidas da seção <c>Oidc</c> do <c>appsettings.json</c>.
/// </summary>
public sealed class OidcOptions
{
    /// <summary>
    /// Authority do provedor OIDC (ex.: "https://accounts.google.com" para GCP WIF).
    /// </summary>
    public string? Authority { get; init; }

    /// <summary>
    /// Audience esperada no token OIDC (URL do endpoint do worker).
    /// </summary>
    public string? Audience { get; init; }

    /// <summary>
    /// E-mail da service account autorizada do Cloud Scheduler (design §10, RNF 7.1).
    /// Identidade não autorizada → 403 DIG-ERR-011.
    /// </summary>
    public string? AuthorizedServiceAccountEmail { get; init; }
}

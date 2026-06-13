namespace TenantAdministration.Domain.Errors;

/// <summary>
/// Catálogo de erros do módulo Tenant Administration.
/// Prefixo TA-ERR conforme design.md §12.
/// </summary>
public static class DomainErrors
{
    /// <summary>Slug em formato inválido; use apenas minúsculas e hífens.</summary>
    public static (string Code, string Message) SlugInvalidFormat =>
        ("TA-ERR-005", "Slug em formato inválido; use apenas minúsculas e hífens (3–40 caracteres, sem hífens nas bordas ou consecutivos).");

    /// <summary>Fuso horário IANA inválido.</summary>
    public static (string Code, string Message) TimezoneInvalidIana =>
        ("TA-ERR-006", "Fuso horário IANA inválido. Use um identificador reconhecido como 'America/Sao_Paulo'.");

    /// <summary>Cor inválida; use formato #RRGGBB.</summary>
    public static (string Code, string Message) ColorInvalidFormat =>
        ("TA-ERR-004", "Cor inválida; use o formato #RRGGBB com letras maiúsculas A–F.");

    /// <summary>Transição de estado inválida.</summary>
    public static (string Code, string Message) InvalidStateTransition =>
        ("TA-ERR-007", "Transição de estado inválida. Verifique o estado atual do tenant.");

    /// <summary>Tenant não encontrado.</summary>
    public static (string Code, string Message) TenantNotFound =>
        ("TA-ERR-008", "Tenant não encontrado. Verifique o identificador informado.");

    /// <summary>Arquivo excede 1 MB.</summary>
    public static (string Code, string Message) AssetTooLarge =>
        ("TA-ERR-012", "Arquivo excede o limite de 1 MB.");

    /// <summary>Formato de arquivo não suportado.</summary>
    public static (string Code, string Message) AssetUnsupportedFormat =>
        ("TA-ERR-013", "Formato de arquivo não suportado. Envie PNG ou SVG para logo e ICO/PNG/SVG para favicon.");

    /// <summary>Contraste insuficiente para WCAG AA.</summary>
    public static (string Code, string Message) WcagContrastInsufficient =>
        ("TA-ERR-014", "Contraste insuficiente para conformidade WCAG AA. Ajuste as cores e verifique a razão de contraste retornada.");
}
